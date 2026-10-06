using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AMIS.API;

public static class AcademicEndpoints {
    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static bool ValidName(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100;

    private static async Task<bool> ValidTeacher(Guid? id, UserManager<AppUser> users) {
        if (id is null) return true;
        var user = await users.FindByIdAsync(id.Value.ToString());
        return user is not null && await users.IsInRoleAsync(user, "Teacher");
    }

    private static async Task<bool> ScheduleOverlap(AppDb db, ScheduleInput input, Guid? except = null) =>
        await db.Schedules.AnyAsync(s => s.Id != except && !s.IsArchived &&
            (s.SectionId == input.SectionId || s.TeacherId == input.TeacherId) &&
            s.StartsAt < input.EndsAt && input.StartsAt < s.EndsAt);

    public static void MapAcademics(this WebApplication app) {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/grades", async (AppDb db, ClaimsPrincipal principal, bool? includeArchived) => {
            var admin = principal.IsInRole("Administrator");
            var id = UserId(principal);
            return await db.GradeLevels
                .Where(g => (includeArchived == true || !g.IsArchived) &&
                    (admin || g.Sections.Any(s => s.AdviserId == id || db.Schedules.Any(sc => sc.SectionId == s.Id && sc.TeacherId == id))))
                .OrderBy(g => g.Name)
                .Select(g => new {
                    g.Id, g.Name, g.Department, g.IsArchived,
                    sections = g.Sections.Count(s => !s.IsArchived),
                    students = g.Sections.SelectMany(s => s.Students).Count(s => !s.IsDeleted)
                }).ToListAsync();
        }).RequireAuthorization("Staff");

        api.MapPost("/grades", async (GradeInput input, AppDb db) => {
            if (!ValidName(input.Name) || !ValidName(input.Department)) return Results.BadRequest(new { message = "Grade and department are required (100 characters maximum)." });
            if (await db.GradeLevels.AnyAsync(g => g.Name.ToLower() == input.Name.Trim().ToLower())) return Results.Conflict(new { message = "A grade with this name already exists. Restore it if archived." });
            var grade = new GradeLevel { Name = input.Name.Trim(), Department = input.Department.Trim() };
            db.GradeLevels.Add(grade);
            await db.SaveChangesAsync();
            return Results.Created($"/api/grades/{grade.Id}", new { grade.Id });
        }).RequireAuthorization("Administrator");

        api.MapPut("/grades/{id:guid}", async (Guid id, GradeInput input, AppDb db) => {
            var grade = await db.GradeLevels.FindAsync(id);
            if (grade is null) return Results.NotFound();
            if (!ValidName(input.Name) || !ValidName(input.Department)) return Results.BadRequest(new { message = "Grade and department are required (100 characters maximum)." });
            if (await db.GradeLevels.AnyAsync(g => g.Id != id && g.Name.ToLower() == input.Name.Trim().ToLower())) return Results.Conflict(new { message = "Grade name already exists." });
            grade.Name = input.Name.Trim();
            grade.Department = input.Department.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(new { grade.Id });
        }).RequireAuthorization("Administrator");

        api.MapDelete("/grades/{id:guid}", async (Guid id, AppDb db) => {
            var grade = await db.GradeLevels.FindAsync(id);
            if (grade is null) return Results.NotFound();
            if (await db.Sections.AnyAsync(s => s.GradeLevelId == id)) grade.IsArchived = true;
            else db.GradeLevels.Remove(grade);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Administrator");

        api.MapPost("/grades/{id:guid}/restore", async (Guid id, AppDb db) => {
            var grade = await db.GradeLevels.FindAsync(id);
            if (grade is null) return Results.NotFound();
            grade.IsArchived = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Administrator");

        api.MapGet("/sections", async (AppDb db, ClaimsPrincipal principal, bool? includeArchived) => {
            var admin = principal.IsInRole("Administrator");
            var id = UserId(principal);
            return await db.Sections
                .Where(s => (includeArchived == true || !s.IsArchived && !s.GradeLevel.IsArchived) &&
                    (admin || s.AdviserId == id || db.Schedules.Any(sc => sc.SectionId == s.Id && sc.TeacherId == id)))
                .OrderBy(s => s.GradeLevel.Name).ThenBy(s => s.Name)
                .Select(s => new {
                    s.Id, s.Name, s.Room, s.GradeLevelId, s.AdviserId, s.IsArchived,
                    grade = s.GradeLevel.Name,
                    adviser = s.Adviser == null ? "" : s.Adviser.FullName,
                    students = s.Students.Count(st => !st.IsDeleted)
                }).ToListAsync();
        }).RequireAuthorization("Staff");

        api.MapPost("/sections", async (SectionInput input, AppDb db, UserManager<AppUser> users) => {
            if (!ValidName(input.Name) || input.Room.Trim().Length > 100) return Results.BadRequest(new { message = "A section name is required; room may be up to 100 characters." });
            if (!await db.GradeLevels.AnyAsync(g => g.Id == input.GradeLevelId && !g.IsArchived)) return Results.BadRequest(new { message = "Select an active grade." });
            if (!await ValidTeacher(input.AdviserId, users)) return Results.BadRequest(new { message = "Adviser must be a teacher." });
            if (await db.Sections.AnyAsync(s => s.GradeLevelId == input.GradeLevelId && s.Name.ToLower() == input.Name.Trim().ToLower())) return Results.Conflict(new { message = "Section name already exists in this grade." });
            var section = new Section { Name = input.Name.Trim(), GradeLevelId = input.GradeLevelId, Room = input.Room.Trim(), AdviserId = input.AdviserId };
            db.Sections.Add(section);
            await db.SaveChangesAsync();
            return Results.Created($"/api/sections/{section.Id}", new { section.Id });
        }).RequireAuthorization("Administrator");

        api.MapPut("/sections/{id:guid}", async (Guid id, SectionInput input, AppDb db, UserManager<AppUser> users) => {
            var section = await db.Sections.FindAsync(id);
            if (section is null) return Results.NotFound();
            if (!ValidName(input.Name) || input.Room.Trim().Length > 100) return Results.BadRequest(new { message = "A section name is required; room may be up to 100 characters." });
            if (!await db.GradeLevels.AnyAsync(g => g.Id == input.GradeLevelId && !g.IsArchived)) return Results.BadRequest(new { message = "Select an active grade." });
            if (!await ValidTeacher(input.AdviserId, users)) return Results.BadRequest(new { message = "Adviser must be a teacher." });
            if (section.GradeLevelId != input.GradeLevelId && (await db.Attendance.AnyAsync(a => a.Schedule.SectionId == id) || await db.AttendanceSubmissions.AnyAsync(a => a.Schedule.SectionId == id)))
                return Results.Conflict(new { message = "A section with attendance history cannot move to another grade. Archive it and create a new section." });
            if (await db.Sections.AnyAsync(s => s.Id != id && s.GradeLevelId == input.GradeLevelId && s.Name.ToLower() == input.Name.Trim().ToLower())) return Results.Conflict(new { message = "Section name already exists in this grade." });
            section.Name = input.Name.Trim();
            section.Room = input.Room.Trim();
            section.GradeLevelId = input.GradeLevelId;
            section.AdviserId = input.AdviserId;
            await db.SaveChangesAsync();
            return Results.Ok(new { section.Id });
        }).RequireAuthorization("Administrator");

        api.MapDelete("/sections/{id:guid}", async (Guid id, AppDb db) => {
            var section = await db.Sections.FindAsync(id);
            if (section is null) return Results.NotFound();
            if (await db.Students.AnyAsync(s => s.SectionId == id) || await db.Schedules.AnyAsync(s => s.SectionId == id)) section.IsArchived = true;
            else db.Sections.Remove(section);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Administrator");

        api.MapPost("/sections/{id:guid}/restore", async (Guid id, AppDb db) => {
            var section = await db.Sections.Include(s => s.GradeLevel).FirstOrDefaultAsync(s => s.Id == id);
            if (section is null) return Results.NotFound();
            if (section.GradeLevel.IsArchived) return Results.Conflict(new { message = "Restore the grade before this section." });
            section.IsArchived = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Administrator");

        api.MapGet("/schedules", async (AppDb db, ClaimsPrincipal principal, bool? includeArchived) => {
            var admin = principal.IsInRole("Administrator");
            var id = UserId(principal);
            return await db.Schedules
                .Where(s => (includeArchived == true || !s.IsArchived && !s.Section.IsArchived && !s.Section.GradeLevel.IsArchived) &&
                    (admin || s.TeacherId == id))
                .OrderBy(s => s.Section.GradeLevel.Name).ThenBy(s => s.Section.Name).ThenBy(s => s.StartsAt)
                .Select(s => new {
                    s.Id, s.Subject, s.SectionId, s.TeacherId, s.StartsAt, s.EndsAt, s.IsArchived,
                    section = s.Section.Name, grade = s.Section.GradeLevel.Name,
                    teacher = s.Teacher.FullName, room = s.Section.Room
                }).ToListAsync();
        }).RequireAuthorization("Staff");

        api.MapPost("/schedules", async (ScheduleInput input, AppDb db, UserManager<AppUser> users) => {
            if (!ValidName(input.Subject) || input.StartsAt >= input.EndsAt) return Results.BadRequest(new { message = "Enter a subject and a valid time range." });
            if (!await db.Sections.AnyAsync(s => s.Id == input.SectionId && !s.IsArchived && !s.GradeLevel.IsArchived)) return Results.BadRequest(new { message = "Select an active section." });
            if (!await ValidTeacher(input.TeacherId, users)) return Results.BadRequest(new { message = "Assigned user must be a teacher." });
            if (await ScheduleOverlap(db, input)) return Results.Conflict(new { message = "This section or teacher already has an overlapping schedule." });
            var schedule = new Schedule { SectionId = input.SectionId, TeacherId = input.TeacherId, Subject = input.Subject.Trim(), StartsAt = input.StartsAt, EndsAt = input.EndsAt };
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
            return Results.Created($"/api/schedules/{schedule.Id}", new { schedule.Id });
        }).RequireAuthorization("Administrator");

        api.MapPut("/schedules/{id:guid}", async (Guid id, ScheduleInput input, AppDb db, UserManager<AppUser> users) => {
            var schedule = await db.Schedules.FindAsync(id);
            if (schedule is null) return Results.NotFound();
            if (!ValidName(input.Subject) || input.StartsAt >= input.EndsAt) return Results.BadRequest(new { message = "Enter a subject and a valid time range." });
            if (!await db.Sections.AnyAsync(s => s.Id == input.SectionId && !s.IsArchived && !s.GradeLevel.IsArchived)) return Results.BadRequest(new { message = "Select an active section." });
            if (!await ValidTeacher(input.TeacherId, users)) return Results.BadRequest(new { message = "Assigned user must be a teacher." });
            var changed = schedule.SectionId != input.SectionId || schedule.TeacherId != input.TeacherId ||
                schedule.Subject != input.Subject.Trim() || schedule.StartsAt != input.StartsAt || schedule.EndsAt != input.EndsAt;
            if (changed && (await db.Attendance.AnyAsync(a => a.ScheduleId == id) || await db.AttendanceSubmissions.AnyAsync(a => a.ScheduleId == id)))
                return Results.Conflict(new { message = "A schedule with attendance history is fixed. Archive it and create a new schedule." });
            if (await ScheduleOverlap(db, input, id)) return Results.Conflict(new { message = "This section or teacher already has an overlapping schedule." });
            schedule.SectionId = input.SectionId;
            schedule.TeacherId = input.TeacherId;
            schedule.Subject = input.Subject.Trim();
            schedule.StartsAt = input.StartsAt;
            schedule.EndsAt = input.EndsAt;
            await db.SaveChangesAsync();
            return Results.Ok(new { schedule.Id });
        }).RequireAuthorization("Administrator");

        api.MapDelete("/schedules/{id:guid}", async (Guid id, AppDb db) => {
            var schedule = await db.Schedules.FindAsync(id);
            if (schedule is null) return Results.NotFound();
            if (await db.Attendance.AnyAsync(a => a.ScheduleId == id) || await db.AttendanceSubmissions.AnyAsync(a => a.ScheduleId == id)) schedule.IsArchived = true;
            else db.Schedules.Remove(schedule);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Administrator");

        api.MapPost("/schedules/{id:guid}/restore", async (Guid id, AppDb db) => {
            var schedule = await db.Schedules.Include(s => s.Section).ThenInclude(s => s.GradeLevel).FirstOrDefaultAsync(s => s.Id == id);
            if (schedule is null) return Results.NotFound();
            if (schedule.Section.IsArchived || schedule.Section.GradeLevel.IsArchived) return Results.Conflict(new { message = "Restore the grade and section first." });
            var input = new ScheduleInput(schedule.SectionId, schedule.TeacherId, schedule.Subject, schedule.StartsAt, schedule.EndsAt);
            if (await ScheduleOverlap(db, input, id)) return Results.Conflict(new { message = "This section or teacher has an overlapping active schedule." });
            schedule.IsArchived = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("Administrator");
    }
}
