using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AMIS.API;

public sealed record GradeInput(string Name, string Department);
public sealed record SectionInput(string Name, Guid GradeLevelId, string Room, Guid? AdviserId);
public sealed record ScheduleInput(Guid SectionId, Guid TeacherId, string Subject, TimeOnly StartsAt, TimeOnly EndsAt, DayOfWeek? DayOfWeek = null);
public sealed record StudentInput(string FirstName, string? MiddleName, string LastName, string? Suffix, Guid? SectionId, string GuardianName, string GuardianEmail);
public sealed record AttendanceInput(Guid StudentId, AttendanceStatus Status, string? Reason = null);
public sealed record AttendanceBatch(DateOnly Date, List<AttendanceInput> Records, Guid SubmissionKey, int ExpectedRevision, string? CorrectionReason);

public static class SchoolEndpoints {
    public static string NewStudentNumber(long number) => $"STD-{number:D7}";
    public static int? AttendanceRate(int present, int excused, int total) =>
        total == 0 ? null : (int)Math.Round(100.0 * (present + excused) / total, MidpointRounding.AwayFromZero);
    public static async Task<string> NextStudentNumber(AppDb db) {
        var number = await db.Database.SqlQueryRaw<long>("SELECT nextval('student_number_sequence') AS \"Value\"").SingleAsync();
        return NewStudentNumber(number);
    }
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static void MapSchool(this WebApplication app) {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/dashboard", async (AppDb db, ClaimsPrincipal user) => {
            var isAdmin = user.IsInRole("Administrator"); var id = UserId(user);
            if (!isAdmin) {
                var assignedStudents = await db.Students.CountAsync(x => !x.IsDeleted &&
                    (x.Section.AdviserId == id || db.Schedules.Any(s => s.SectionId == x.SectionId && s.TeacherId == id && !s.IsArchived)));
                var assignedGuardians = await db.GuardianStudents
                    .Where(x => !x.Student.IsDeleted && !x.Student.Section.IsArchived && !x.Student.Section.GradeLevel.IsArchived &&
                        db.Schedules.Any(s => s.SectionId == x.Student.SectionId && s.TeacherId == id && !s.IsArchived))
                    .Select(x => x.GuardianId).Distinct().CountAsync();
                var assignedSubjects = await db.Schedules
                    .Where(s => s.TeacherId == id && !s.IsArchived && !s.Section.IsArchived && !s.Section.GradeLevel.IsArchived)
                    .Select(s => s.Subject).Distinct().CountAsync();
                return Results.Ok(new { students = assignedStudents, guardians = assignedGuardians, subjects = assignedSubjects });
            }
            var sections = await db.Sections.CountAsync(x => isAdmin || x.AdviserId == id || db.Schedules.Any(s => s.SectionId == x.Id && s.TeacherId == id));
            var students = await db.Students.CountAsync(x => !x.IsDeleted && (isAdmin || x.Section.AdviserId == id || db.Schedules.Any(s => s.SectionId == x.SectionId && s.TeacherId == id)));
            var today = AttendanceEndpoints.SchoolToday();
            var present = await db.Attendance.CountAsync(x => x.Date == today && x.Status == AttendanceStatus.Present && (isAdmin || x.Schedule.TeacherId == id));
            var absent = await db.Attendance.CountAsync(x => x.Date == today && x.Status == AttendanceStatus.Absent && (isAdmin || x.Schedule.TeacherId == id));
            var guardians = await db.GuardianStudents.Where(x => !x.Student.IsDeleted &&
                (isAdmin || x.Student.Section.AdviserId == id || db.Schedules.Any(s => s.SectionId == x.Student.SectionId && s.TeacherId == id)))
                .Select(x => x.GuardianId).Distinct().CountAsync();
            var teachers = isAdmin
                ? await db.UserRoles.CountAsync(x => db.Roles.Any(r => r.Id == x.RoleId && r.Name == "Teacher"))
                : 1;
            var subjects = await db.Schedules.Where(x => !x.IsArchived && !x.Section.IsArchived && !x.Section.GradeLevel.IsArchived)
                .Select(x => x.Subject).Distinct().CountAsync();
            return Results.Ok(new { students, sections, attendanceToday = present, absentToday = absent, alerts = await db.AlertLogs.CountAsync(), guardians, teachers, subjects });
        }).RequireAuthorization("Staff");
        api.MapGet("/dashboard/trend", async (string? period, AppDb db, ClaimsPrincipal user) => {
            if (period is not (null or "day" or "week" or "month"))
                return Results.BadRequest(new { message = "Choose Day, Week, or Month." });
            var selected = period ?? "day";
            var today = AttendanceEndpoints.SchoolToday();
            var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var start = selected switch {
                "week" => weekStart.AddDays(-49),
                "month" => new DateOnly(today.Year, today.Month, 1).AddMonths(-11),
                _ => today.AddDays(-6)
            };
            var bucketCount = selected switch { "week" => 8, "month" => 12, _ => 7 };
            var isAdmin = user.IsInRole("Administrator"); var id = UserId(user);
            var totals = await db.Attendance.Where(x => x.Date >= start && x.Date <= today && (isAdmin || x.Schedule.TeacherId == id))
                .GroupBy(x => x.Date)
                .Select(g => new { date = g.Key, present = g.Count(x => x.Status == AttendanceStatus.Present),
                    absent = g.Count(x => x.Status == AttendanceStatus.Absent), records = g.Count() })
                .ToListAsync();
            var points = Enumerable.Range(0, bucketCount).Select(index => {
                var date = selected switch {
                    "week" => start.AddDays(index * 7),
                    "month" => start.AddMonths(index),
                    _ => start.AddDays(index)
                };
                var end = selected switch {
                    "week" => date.AddDays(6),
                    "month" => date.AddMonths(1).AddDays(-1),
                    _ => date
                };
                var bucket = totals.Where(x => x.date >= date && x.date <= end).ToList();
                return new { date, present = bucket.Sum(x => x.present), absent = bucket.Sum(x => x.absent), records = bucket.Sum(x => x.records) };
            });
            return Results.Ok(new { period = selected, points });
        }).RequireAuthorization("Staff");
        api.MapGet("/reports", async (Guid scheduleId, int year, int month, AppDb db, ClaimsPrincipal user) => { var schedule = await db.Schedules.FindAsync(scheduleId); if (schedule is null) return Results.NotFound(); if (user.IsInRole("Teacher") && schedule.TeacherId != UserId(user)) return Results.Forbid(); if (month < 1 || month > 12) return Results.BadRequest(); var rows = await db.Students.Where(s => (s.SectionId == schedule.SectionId && !s.IsDeleted) || db.Attendance.Any(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month)).Select(s => new { s.FirstName, s.LastName, present = db.Attendance.Count(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month && a.Status == AttendanceStatus.Present), absent = db.Attendance.Count(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month && a.Status == AttendanceStatus.Absent), late = db.Attendance.Count(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month && a.Status == AttendanceStatus.Late), excused = db.Attendance.Count(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month && a.Status == AttendanceStatus.Excused) }).ToListAsync(); return Results.Ok(rows); }).RequireAuthorization("Staff");
        api.MapGet("/reports/summary", async (Guid sectionId, DateOnly startDate, DateOnly endDate, AppDb db, ClaimsPrincipal user) => {
            if (startDate > endDate || endDate.DayNumber - startDate.DayNumber > 366)
                return Results.BadRequest(new { message = "Choose a valid reporting period of at most one year." });
            var isAdmin = user.IsInRole("Administrator"); var id = UserId(user);
            if (!await db.Sections.AnyAsync(s => s.Id == sectionId && (isAdmin || s.AdviserId == id || db.Schedules.Any(sc => sc.SectionId == s.Id && sc.TeacherId == id))))
                return Results.NotFound();
            var rows = await db.Schedules.Where(s => s.SectionId == sectionId && !s.IsArchived && (isAdmin || s.TeacherId == id))
                .SelectMany(schedule => db.Students.Where(student => (student.SectionId == schedule.SectionId && !student.IsDeleted) || db.Attendance.Any(a => a.StudentId == student.Id && a.ScheduleId == schedule.Id && a.Date >= startDate && a.Date <= endDate)),
                    (schedule, student) => new {
                        schedule.Id, schedule.Subject, student.StudentNumber, student.FirstName, student.LastName,
                        present = db.Attendance.Count(a => a.ScheduleId == schedule.Id && a.StudentId == student.Id && a.Date >= startDate && a.Date <= endDate && a.Status == AttendanceStatus.Present),
                        absent = db.Attendance.Count(a => a.ScheduleId == schedule.Id && a.StudentId == student.Id && a.Date >= startDate && a.Date <= endDate && a.Status == AttendanceStatus.Absent),
                        late = db.Attendance.Count(a => a.ScheduleId == schedule.Id && a.StudentId == student.Id && a.Date >= startDate && a.Date <= endDate && a.Status == AttendanceStatus.Late),
                        excused = db.Attendance.Count(a => a.ScheduleId == schedule.Id && a.StudentId == student.Id && a.Date >= startDate && a.Date <= endDate && a.Status == AttendanceStatus.Excused)
                    }).OrderBy(x => x.Subject).ThenBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync();
            return Results.Ok(rows);
        }).RequireAuthorization("Staff");
        api.MapGet("/users", async (UserManager<AppUser> users, AppDb db) => { var all = await users.Users.OrderBy(u => u.FullName).ToListAsync(); var childCounts = await db.GuardianStudents.Where(x => !x.Student.IsDeleted).GroupBy(x => x.GuardianId).Select(x => new { id = x.Key, count = x.Count() }).ToDictionaryAsync(x => x.id, x => x.count); var list = new List<object>(); foreach(var u in all) { var roles = await users.GetRolesAsync(u); var role = roles.Contains("Administrator") ? "Administrator" : roles.Contains("Teacher") ? "Teacher" : roles.FirstOrDefault() ?? "Unknown"; list.Add(new { u.Id, u.FullName, u.Email, u.EmailConfirmed, role, roles, children = roles.Contains("Guardian") ? childCounts.GetValueOrDefault(u.Id) : 0 }); } return Results.Ok(list); }).RequireAuthorization("Administrator");
        api.MapGet("/alerts", async (int? page, AppDb db) => {
            const int pageSize = 20;
            var total = await db.AlertLogs.CountAsync();
            var currentPage = Math.Clamp(page ?? 1, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
            var items = await db.AlertLogs.AsNoTracking()
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((currentPage - 1) * pageSize).Take(pageSize).ToListAsync();
            return Results.Ok(new { items, total, page = currentPage, pageSize });
        }).RequireAuthorization("Administrator");
    }
}
