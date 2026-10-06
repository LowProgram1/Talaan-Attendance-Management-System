using System.Data;
using System.Net;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AMIS.API;

public static class AttendanceEndpoints {
    private static readonly TimeZoneInfo SchoolTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static DateOnly SchoolToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, SchoolTimeZone).DateTime);
    public static bool ValidExcuseReasons(IEnumerable<AttendanceInput> records) =>
        records.All(r => r.Status != AttendanceStatus.Excused ||
            !string.IsNullOrWhiteSpace(r.Reason) && r.Reason.Trim().Length <= 500);
    public static string NotificationBody(Student student, string subject, DateOnly date, AttendanceStatus status, string? reason) {
        var body = $"<p>{WebUtility.HtmlEncode(student.FirstName)} {WebUtility.HtmlEncode(student.LastName)} was marked <strong>{status.ToString().ToLowerInvariant()}</strong> in <strong>{WebUtility.HtmlEncode(subject)}</strong> on {date:MMMM d, yyyy}.</p>";
        return status == AttendanceStatus.Excused
            ? body + $"<p>Reason: {WebUtility.HtmlEncode(reason?.Trim())}</p>"
            : body;
    }

    public static void MapAttendance(this WebApplication app) {
        var api = app.MapGroup("/api").RequireAuthorization("Staff");

        api.MapGet("/attendance/{scheduleId:guid}", async (Guid scheduleId, DateOnly? date, AppDb db, ClaimsPrincipal principal) => {
            var schedule = await db.Schedules.Include(s => s.Section).ThenInclude(s => s.GradeLevel)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);
            if (schedule is null || schedule.IsArchived || schedule.Section.IsArchived || schedule.Section.GradeLevel.IsArchived)
                return Results.NotFound();
            if (principal.IsInRole("Teacher") && schedule.TeacherId != UserId(principal)) return Results.Forbid();
            var day = date ?? SchoolToday();
            var revision = await db.AttendanceSubmissions
                .Where(s => s.ScheduleId == scheduleId && s.Date == day)
                .Select(s => (int?)s.Revision).MaxAsync() ?? 0;
            var roster = await db.Students.Where(s => s.SectionId == schedule.SectionId && !s.IsDeleted)
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .Select(s => new {
                    s.Id, s.StudentNumber, s.FirstName, s.LastName,
                    guardians = s.Guardians.Select(g => g.Guardian.FullName).ToList(),
                    status = db.Attendance.Where(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date == day)
                        .Select(a => (AttendanceStatus?)a.Status).FirstOrDefault(),
                    reason = db.Attendance.Where(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date == day)
                        .Select(a => a.ExcuseReason).FirstOrDefault()
                }).ToListAsync();
            return Results.Ok(new { date = day, revision, roster });
        });

        api.MapPut("/attendance/{scheduleId:guid}", async (Guid scheduleId, AttendanceBatch input,
            AppDb db, ClaimsPrincipal principal, EmailService email, ILogger<EmailService> logger) => {
            var schedule = await db.Schedules.Include(s => s.Section).ThenInclude(s => s.GradeLevel)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);
            if (schedule is null || schedule.IsArchived || schedule.Section.IsArchived || schedule.Section.GradeLevel.IsArchived)
                return Results.NotFound();
            var teacher = principal.IsInRole("Teacher");
            if (teacher && schedule.TeacherId != UserId(principal)) return Results.Forbid();
            var today = SchoolToday();
            if (input.Date > today || teacher && input.Date != today)
                return Results.BadRequest(new { message = "Teachers may submit attendance only for the current school day." });
            if (input.Date < today && !teacher && string.IsNullOrWhiteSpace(input.CorrectionReason))
                return Results.BadRequest(new { message = "A reason is required for a past-day correction." });
            if (input.SubmissionKey == Guid.Empty || input.ExpectedRevision < 0 || input.Records is null ||
                input.Records.Count == 0 || input.Records.Select(r => r.StudentId).Distinct().Count() != input.Records.Count ||
                input.Records.Any(r => !Enum.IsDefined(r.Status)))
                return Results.BadRequest(new { message = "Invalid attendance submission." });
            if (!ValidExcuseReasons(input.Records))
                return Results.BadRequest(new { message = "Every excused student needs a reason of up to 500 characters." });

            var previous = await db.AttendanceSubmissions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.IdempotencyKey == input.SubmissionKey);
            if (previous is not null)
                return previous.ScheduleId == scheduleId && previous.Date == input.Date
                    ? Results.Ok(new { saved = input.Records.Count, revision = previous.Revision, submissionId = previous.Id })
                    : Results.Conflict(new { message = "Submission key was already used." });

            var roster = await db.Students.Include(s => s.Guardians).ThenInclude(g => g.Guardian)
                .Where(s => s.SectionId == schedule.SectionId && !s.IsDeleted)
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync();
            if (roster.Count != input.Records.Count ||
                !roster.Select(s => s.Id).Order().SequenceEqual(input.Records.Select(r => r.StudentId).Order()))
                return Results.BadRequest(new { message = "Submit a status for every current student in this section. Refresh the roster and try again." });

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try {
                var latestRevision = await db.AttendanceSubmissions
                    .Where(s => s.ScheduleId == scheduleId && s.Date == input.Date)
                    .Select(s => (int?)s.Revision).MaxAsync() ?? 0;
                if (latestRevision != input.ExpectedRevision)
                    return Results.Conflict(new { message = "Attendance was updated elsewhere. Refresh the class records before saving." });

                var statuses = input.Records.ToDictionary(r => r.StudentId, r => r.Status);
                var reasons = input.Records.ToDictionary(r => r.StudentId,
                    r => r.Status == AttendanceStatus.Excused ? r.Reason!.Trim() : null);
                var existing = await db.Attendance
                    .Where(a => a.ScheduleId == scheduleId && a.Date == input.Date)
                    .ToDictionaryAsync(a => a.StudentId);
                var changed = roster.Where(student => !existing.TryGetValue(student.Id, out var prior) ||
                    prior.Status != statuses[student.Id] || prior.ExcuseReason != reasons[student.Id]).ToList();
                foreach (var student in roster) {
                    var status = statuses[student.Id];
                    if (existing.TryGetValue(student.Id, out var record)) {
                        record.Status = status;
                        record.ExcuseReason = reasons[student.Id];
                        record.RecordedById = UserId(principal);
                        record.UpdatedAt = DateTimeOffset.UtcNow;
                    } else {
                        db.Attendance.Add(new AttendanceRecord {
                            StudentId = student.Id, ScheduleId = scheduleId, Date = input.Date,
                            Status = status, ExcuseReason = reasons[student.Id], RecordedById = UserId(principal)
                        });
                    }
                }
                var submission = new AttendanceSubmission {
                    ScheduleId = scheduleId,
                    Date = input.Date,
                    SubmittedById = UserId(principal),
                    Revision = latestRevision + 1,
                    IdempotencyKey = input.SubmissionKey,
                    CorrectionReason = string.IsNullOrWhiteSpace(input.CorrectionReason) ? null : input.CorrectionReason.Trim(),
                    PresentCount = statuses.Values.Count(s => s == AttendanceStatus.Present),
                    AbsentCount = statuses.Values.Count(s => s == AttendanceStatus.Absent),
                    LateCount = statuses.Values.Count(s => s == AttendanceStatus.Late),
                    ExcusedCount = statuses.Values.Count(s => s == AttendanceStatus.Excused),
                    Items = roster.Select(student => new AttendanceSubmissionItem {
                        StudentId = student.Id,
                        StudentNumber = student.StudentNumber,
                        StudentName = $"{student.LastName}, {student.FirstName}",
                        GuardianNames = string.Join(", ", student.Guardians.Select(g => g.Guardian.FullName)),
                        Status = statuses[student.Id],
                        ExcuseReason = reasons[student.Id]
                    }).ToList()
                };
                db.AttendanceSubmissions.Add(submission);
                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                await SendAlerts(input, schedule.Subject, changed, statuses, reasons, db, email, logger);
                return Results.Ok(new { saved = roster.Count, revision = submission.Revision, submissionId = submission.Id });
            } catch (DbUpdateException) {
                await transaction.RollbackAsync();
                return Results.Conflict(new { message = "Attendance changed while you were saving. Refresh and try again." });
            } catch (PostgresException ex) when (ex.SqlState is "40001" or "23505") {
                await transaction.RollbackAsync();
                return Results.Conflict(new { message = "Attendance changed while you were saving. Refresh and try again." });
            }
        });

        api.MapGet("/reports/submissions", async (Guid sectionId, int year, int month, int? page,
            AppDb db, ClaimsPrincipal principal) => {
            if (year < 2000 || year > 2100 || month is < 1 or > 12) return Results.BadRequest(new { message = "Select a valid month." });
            var admin = principal.IsInRole("Administrator");
            var query = db.AttendanceSubmissions.AsNoTracking()
                .Where(s => s.Schedule.SectionId == sectionId && s.Date.Year == year && s.Date.Month == month);
            if (!admin) query = query.Where(s => s.Schedule.TeacherId == UserId(principal));
            var total = await query.CountAsync();
            var currentPage = Math.Max(1, page ?? 1);
            var items = await query.OrderByDescending(s => s.Date).ThenByDescending(s => s.SubmittedAt)
                .Skip((currentPage - 1) * 20).Take(20)
                .Select(s => new {
                    s.Id, s.Date, s.SubmittedAt, s.Revision, s.CorrectionReason,
                    s.PresentCount, s.AbsentCount, s.LateCount, s.ExcusedCount,
                    s.ScheduleId, subject = s.Schedule.Subject, teacher = s.SubmittedBy.FullName,
                    grade = s.Schedule.Section.GradeLevel.Name, section = s.Schedule.Section.Name
                }).ToListAsync();
            return Results.Ok(new { items, total, page = currentPage, pageSize = 20 });
        });

        api.MapGet("/reports/submissions/{id:guid}", async (Guid id, AppDb db, ClaimsPrincipal principal) => {
            var submission = await db.AttendanceSubmissions.AsNoTracking()
                .Where(s => s.Id == id).Select(s => new {
                    s.Id, s.Date, s.SubmittedAt, s.Revision, s.CorrectionReason,
                    s.PresentCount, s.AbsentCount, s.LateCount, s.ExcusedCount,
                    teacherId = s.Schedule.TeacherId, subject = s.Schedule.Subject,
                    teacher = s.SubmittedBy.FullName, grade = s.Schedule.Section.GradeLevel.Name,
                    section = s.Schedule.Section.Name,
                    items = s.Items.OrderBy(i => i.StudentName).Select(i => new {
                        i.StudentId, i.StudentNumber, i.StudentName, i.GuardianNames, i.Status, i.ExcuseReason
                    }).ToList()
                }).FirstOrDefaultAsync();
            if (submission is null) return Results.NotFound();
            if (principal.IsInRole("Teacher") && submission.teacherId != UserId(principal)) return Results.Forbid();
            return Results.Ok(submission);
        });
    }

    private static async Task SendAlerts(AttendanceBatch input, string subject, List<Student> changed,
        Dictionary<Guid, AttendanceStatus> statuses, Dictionary<Guid, string?> reasons,
        AppDb db, EmailService email, ILogger<EmailService> logger) {
        foreach (var student in changed) {
            foreach (var guardian in student.Guardians.Select(g => g.Guardian).Where(g => !string.IsNullOrWhiteSpace(g.Email))) {
                var status = statuses[student.Id].ToString();
                var alert = new AlertLog { StudentId = student.Id, Recipient = guardian.Email!, Status = status };
                db.AlertLogs.Add(alert);
                try {
                    await email.SendAsync(guardian.Email!, $"Attendance update: {student.FirstName} - {subject}",
                        NotificationBody(student, subject, input.Date, statuses[student.Id], reasons[student.Id]));
                    alert.Delivered = true;
                } catch (Exception ex) {
                    alert.Delivered = false;
                    logger.LogWarning(ex, "Attendance alert delivery failed for student {StudentId}", student.Id);
                }
            }
        }
        await db.SaveChangesAsync();
    }
}
