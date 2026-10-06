using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AMIS.API;

public sealed record GradeInput(string Name, string Department);
public sealed record SectionInput(string Name, Guid GradeLevelId, string Room, Guid? AdviserId);
public sealed record ScheduleInput(Guid SectionId, Guid TeacherId, string Subject, TimeOnly StartsAt, TimeOnly EndsAt);
public sealed record StudentInput(string FirstName, string? MiddleName, string LastName, string? Suffix, Guid SectionId, string GuardianName, string GuardianEmail);
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
            var guardians = await db.GuardianStudents.Where(x => !x.Student.IsDeleted &&
                (isAdmin || x.Student.Section.AdviserId == id || db.Schedules.Any(s => s.SectionId == x.Student.SectionId && s.TeacherId == id)))
                .Select(x => x.GuardianId).Distinct().CountAsync();
            var teachers = isAdmin
                ? await db.UserRoles.CountAsync(x => db.Roles.Any(r => r.Id == x.RoleId && r.Name == "Teacher"))
                : 1;
            var subjects = await db.Schedules.Where(x => !x.IsArchived && !x.Section.IsArchived && !x.Section.GradeLevel.IsArchived)
                .Select(x => x.Subject).Distinct().CountAsync();
            return Results.Ok(new { students, sections, attendanceToday = present, alerts = await db.AlertLogs.CountAsync(), guardians, teachers, subjects });
        }).RequireAuthorization("Staff");
        api.MapGet("/dashboard/trend", async (string? period, AppDb db, ClaimsPrincipal user) => {
            if (period is not (null or "week" or "month"))
                return Results.BadRequest(new { message = "Choose Week or Month." });
            var selected = period ?? "week";
            var today = AttendanceEndpoints.SchoolToday();
            var days = selected == "month" ? 30 : 7;
            var start = today.AddDays(1 - days);
            var isAdmin = user.IsInRole("Administrator"); var id = UserId(user);
            var totals = await db.Attendance.Where(x => x.Date >= start && x.Date <= today && (isAdmin || x.Schedule.TeacherId == id))
                .GroupBy(x => x.Date)
                .Select(g => new { date = g.Key, present = g.Count(x => x.Status == AttendanceStatus.Present),
                    excused = g.Count(x => x.Status == AttendanceStatus.Excused), total = g.Count() })
                .ToListAsync();
            var byDate = totals.ToDictionary(x => x.date);
            var points = Enumerable.Range(0, days).Select(offset => {
                var date = start.AddDays(offset);
                var found = byDate.GetValueOrDefault(date);
                return new { date, rate = found is null ? null : AttendanceRate(found.present, found.excused, found.total), records = found?.total ?? 0 };
            });
            return Results.Ok(new { period = selected, points });
        }).RequireAuthorization("Staff");
        api.MapGet("/students", async (AppDb db, ClaimsPrincipal user, string? search, Guid? sectionId, int? page, bool? archived) => {
            var query = db.Students.Where(s => user.IsInRole("Administrator") && archived == true ? s.IsDeleted : !s.IsDeleted);
            if (user.IsInRole("Teacher")) query = query.Where(s => s.Section.AdviserId == UserId(user) || db.Schedules.Any(sc => sc.SectionId == s.SectionId && sc.TeacherId == UserId(user)));
            if (sectionId is not null) query = query.Where(s => s.SectionId == sectionId);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(s => s.FirstName.Contains(search) || s.LastName.Contains(search) || s.StudentNumber.Contains(search));
            var total = await query.CountAsync(); var items = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).Skip(Math.Max(0, (page ?? 1) - 1) * 20).Take(20).Select(s => new { s.Id, s.StudentNumber, s.FirstName, s.MiddleName, s.LastName, s.Suffix, s.SectionId, s.IsDeleted, section = s.Section.Name, grade = s.Section.GradeLevel.Name, guardians = s.Guardians.Select(g => new { g.Guardian.FullName, g.Guardian.Email }) }).ToListAsync();
            return Results.Ok(new { items, total, page = page ?? 1 });
        }).RequireAuthorization("Staff");
        api.MapPost("/students", async (StudentInput x, AppDb db, UserManager<AppUser> users, ClaimsPrincipal user) => {
            if (string.IsNullOrWhiteSpace(x.FirstName) || string.IsNullOrWhiteSpace(x.LastName) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(x.GuardianEmail)) return Results.BadRequest(new { message = "Invalid student or guardian details." });
            if (!await db.Sections.AnyAsync(s => s.Id == x.SectionId && !s.IsArchived && !s.GradeLevel.IsArchived && (user.IsInRole("Administrator") || s.AdviserId == UserId(user) || db.Schedules.Any(sc => sc.SectionId == s.Id && sc.TeacherId == UserId(user) && !sc.IsArchived)))) return Results.BadRequest(new { message = "Section not found or not assigned to you." });
            if (await db.Students.AnyAsync(s => s.FirstName.ToLower() == x.FirstName.Trim().ToLower() && s.LastName.ToLower() == x.LastName.Trim().ToLower() && s.SectionId == x.SectionId && s.Guardians.Any(g => g.Guardian.Email == x.GuardianEmail.Trim()))) return Results.Conflict(new { message = "This student already exists in the section." });
            var guardian = await users.FindByEmailAsync(x.GuardianEmail.Trim());
            if (guardian is null) { guardian = new AppUser { UserName = x.GuardianEmail.Trim(), Email = x.GuardianEmail.Trim(), FullName = x.GuardianName.Trim(), IsActive = false }; var result = await users.CreateAsync(guardian); if (!result.Succeeded) return Results.BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) }); await users.AddToRoleAsync(guardian, "Guardian"); }
            else if (!await users.IsInRoleAsync(guardian, "Guardian")) return Results.Conflict(new { message = "Email belongs to another role." });
            var student = new Student { FirstName = x.FirstName.Trim(), MiddleName = x.MiddleName?.Trim(), LastName = x.LastName.Trim(), Suffix = x.Suffix?.Trim(), SectionId = x.SectionId }; student.StudentNumber = await NextStudentNumber(db);
            db.Students.Add(student); db.GuardianStudents.Add(new GuardianStudent { GuardianId = guardian.Id, StudentId = student.Id }); await db.SaveChangesAsync();
            return Results.Created($"/api/students/{student.Id}", new { student.Id });
        }).RequireAuthorization("Staff");
        api.MapPut("/students/{id:guid}", async (Guid id, StudentInput x, AppDb db) => {
            var student = await db.Students.FindAsync(id); if (student is null || student.IsDeleted) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(x.FirstName) || string.IsNullOrWhiteSpace(x.LastName)) return Results.BadRequest(new { message = "Invalid student details." });
            if (!await db.Sections.AnyAsync(s => s.Id == x.SectionId && !s.IsArchived && !s.GradeLevel.IsArchived)) return Results.BadRequest(new { message = "Active section not found." });
            if (await db.Students.AnyAsync(s => s.Id != id && s.FirstName.ToLower() == x.FirstName.Trim().ToLower() && s.LastName.ToLower() == x.LastName.Trim().ToLower() && s.SectionId == x.SectionId && s.Guardians.Any(g => g.Guardian.Email == x.GuardianEmail.Trim()))) return Results.Conflict(new { message = "This student already exists in the section." });
            student.FirstName = x.FirstName.Trim(); student.MiddleName = x.MiddleName?.Trim(); student.LastName = x.LastName.Trim(); student.Suffix = x.Suffix?.Trim(); student.SectionId = x.SectionId;
            await db.SaveChangesAsync(); return Results.Ok(new { student.Id });
        }).RequireAuthorization("Administrator");
        api.MapDelete("/students/{id:guid}", async (Guid id, AppDb db) => { var student = await db.Students.FindAsync(id); if (student is null) return Results.NotFound(); student.IsDeleted = true; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization("Administrator");
        api.MapPost("/students/{id:guid}/restore", async (Guid id, AppDb db) => { var student = await db.Students.FindAsync(id); if (student is null) return Results.NotFound(); student.IsDeleted = false; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization("Administrator");
        api.MapGet("/reports", async (Guid scheduleId, int year, int month, AppDb db, ClaimsPrincipal user) => { var schedule = await db.Schedules.FindAsync(scheduleId); if (schedule is null) return Results.NotFound(); if (user.IsInRole("Teacher") && schedule.TeacherId != UserId(user)) return Results.Forbid(); if (month < 1 || month > 12) return Results.BadRequest(); var rows = await db.Students.Where(s => s.SectionId == schedule.SectionId && !s.IsDeleted).Select(s => new { s.FirstName, s.LastName, present = db.Attendance.Count(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month && a.Status == AttendanceStatus.Present), absent = db.Attendance.Count(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month && a.Status == AttendanceStatus.Absent), late = db.Attendance.Count(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month && a.Status == AttendanceStatus.Late), excused = db.Attendance.Count(a => a.StudentId == s.Id && a.ScheduleId == scheduleId && a.Date.Year == year && a.Date.Month == month && a.Status == AttendanceStatus.Excused) }).ToListAsync(); return Results.Ok(rows); }).RequireAuthorization("Staff");
        api.MapGet("/reports/summary", async (Guid sectionId, DateOnly startDate, DateOnly endDate, AppDb db, ClaimsPrincipal user) => {
            if (startDate > endDate || endDate.DayNumber - startDate.DayNumber > 366)
                return Results.BadRequest(new { message = "Choose a valid reporting period of at most one year." });
            var isAdmin = user.IsInRole("Administrator"); var id = UserId(user);
            if (!await db.Sections.AnyAsync(s => s.Id == sectionId && (isAdmin || s.AdviserId == id || db.Schedules.Any(sc => sc.SectionId == s.Id && sc.TeacherId == id))))
                return Results.NotFound();
            var rows = await db.Schedules.Where(s => s.SectionId == sectionId && !s.IsArchived && (isAdmin || s.TeacherId == id))
                .SelectMany(schedule => db.Students.Where(student => student.SectionId == schedule.SectionId && !student.IsDeleted),
                    (schedule, student) => new {
                        schedule.Id, schedule.Subject, student.StudentNumber, student.FirstName, student.LastName,
                        present = db.Attendance.Count(a => a.ScheduleId == schedule.Id && a.StudentId == student.Id && a.Date >= startDate && a.Date <= endDate && a.Status == AttendanceStatus.Present),
                        absent = db.Attendance.Count(a => a.ScheduleId == schedule.Id && a.StudentId == student.Id && a.Date >= startDate && a.Date <= endDate && a.Status == AttendanceStatus.Absent),
                        late = db.Attendance.Count(a => a.ScheduleId == schedule.Id && a.StudentId == student.Id && a.Date >= startDate && a.Date <= endDate && a.Status == AttendanceStatus.Late),
                        excused = db.Attendance.Count(a => a.ScheduleId == schedule.Id && a.StudentId == student.Id && a.Date >= startDate && a.Date <= endDate && a.Status == AttendanceStatus.Excused)
                    }).OrderBy(x => x.Subject).ThenBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync();
            return Results.Ok(rows);
        }).RequireAuthorization("Staff");
        api.MapGet("/users", async (UserManager<AppUser> users, AppDb db) => { var all = await users.Users.OrderBy(u => u.FullName).ToListAsync(); var childCounts = await db.GuardianStudents.Where(x => !x.Student.IsDeleted).GroupBy(x => x.GuardianId).Select(x => new { id = x.Key, count = x.Count() }).ToDictionaryAsync(x => x.id, x => x.count); var list = new List<object>(); foreach(var u in all) { var role = (await users.GetRolesAsync(u)).FirstOrDefault() ?? "Unknown"; list.Add(new { u.Id, u.FullName, u.Email, u.EmailConfirmed, role, children = role == "Guardian" ? childCounts.GetValueOrDefault(u.Id) : 0 }); } return Results.Ok(list); }).RequireAuthorization("Administrator");
        api.MapGet("/alerts", async (AppDb db) => await db.AlertLogs.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync()).RequireAuthorization("Administrator");
    }
}
