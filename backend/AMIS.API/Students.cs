using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AMIS.API;

public sealed record EnrollmentInput(Guid SectionId, Guid? ExpectedSectionId, bool ConfirmTransfer = false);

public static class StudentRegistry {
    public static string Clean(string? value) => Regex.Replace(value?.Trim() ?? "", @"\s+", " ");
    public static string IdentityKey(string first, string last, string email) =>
        $"{Clean(first).ToUpperInvariant()}|{Clean(last).ToUpperInvariant()}|{email.Trim().ToUpperInvariant()}";

    // All entry points share this transaction lock, including CSV imports.
    public static async Task<IDbContextTransaction> BeginWrite(AppDb db) {
        var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724110030)");
        return transaction;
    }

    public static async Task<Student?> FindDuplicate(AppDb db, string first, string last, string email, Guid? except = null) {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var candidates = await db.Students.Include(s => s.Section).ThenInclude(s => s.GradeLevel)
            .Where(s => s.Id != except && s.Guardians.Any(g => g.Guardian.Email != null && g.Guardian.Email.ToUpper() == normalizedEmail))
            .ToListAsync();
        return candidates.FirstOrDefault(s =>
            string.Equals(Clean(s.FirstName), Clean(first), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Clean(s.LastName), Clean(last), StringComparison.OrdinalIgnoreCase));
    }

    public static string DuplicateMessage(Student s) {
        var placement = s.IsDeleted ? "archived" : s.SectionId == null ? "awaiting enrollment" : $"enrolled in {s.Section.GradeLevel.Name} – {s.Section.Name}";
        return $"{s.FirstName} {s.LastName} ({s.StudentNumber}) already exists and is {placement}. Use the existing Student List record.";
    }

    public static Task<bool> CanEnrollIn(AppDb db, ClaimsPrincipal user, Guid sectionId) {
        var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var admin = user.IsInRole("Administrator");
        return db.Sections.AnyAsync(s => s.Id == sectionId && !s.IsArchived && !s.GradeLevel.IsArchived &&
            (admin || s.AdviserId == id || db.Schedules.Any(sc => sc.SectionId == s.Id && sc.TeacherId == id && !sc.IsArchived)));
    }

    public static bool Valid(StudentInput x) => Clean(x.FirstName).Length is > 0 and <= 100 &&
        Clean(x.LastName).Length is > 0 and <= 100 && Clean(x.GuardianName).Length is > 0 and <= 100 &&
        !string.IsNullOrWhiteSpace(x.GuardianEmail) && new EmailAddressAttribute().IsValid(x.GuardianEmail);

    public static async Task<(AppUser? Guardian, string? Error)> Guardian(UserManager<AppUser> users, string name, string email) {
        email = email.Trim();
        var guardian = await users.FindByEmailAsync(email);
        if (guardian != null) return await users.IsInRoleAsync(guardian, "Guardian")
            ? (guardian, null) : (null, "Guardian email belongs to another role.");
        guardian = new AppUser { UserName = email, Email = email, FullName = Clean(name), IsActive = false };
        var result = await users.CreateAsync(guardian);
        if (!result.Succeeded) return (null, string.Join(" ", result.Errors.Select(e => e.Description)));
        result = await users.AddToRoleAsync(guardian, "Guardian");
        return result.Succeeded ? (guardian, null) : (null, "Unable to create guardian contact.");
    }

    public static async Task<(Guid? Id, string? Error, bool Conflict)> Create(StudentInput x, AppDb db, UserManager<AppUser> users, ClaimsPrincipal user) {
        if (!Valid(x)) return (null, "Enter valid student names, guardian name, and guardian email.", false);
        if (x.SectionId.HasValue && !await CanEnrollIn(db, user, x.SectionId.Value))
            return (null, "Section not found or not assigned to you.", false);
        await using var transaction = await BeginWrite(db);
        var duplicate = await FindDuplicate(db, x.FirstName, x.LastName, x.GuardianEmail);
        if (duplicate != null) return (null, DuplicateMessage(duplicate), true);
        var (guardian, error) = await Guardian(users, x.GuardianName, x.GuardianEmail);
        if (guardian == null) return (null, error, false);
        var student = new Student {
            FirstName = Clean(x.FirstName), MiddleName = Clean(x.MiddleName), LastName = Clean(x.LastName),
            Suffix = Clean(x.Suffix), SectionId = x.SectionId, StudentNumber = await SchoolEndpoints.NextStudentNumber(db)
        };
        db.Students.Add(student);
        db.GuardianStudents.Add(new GuardianStudent { GuardianId = guardian.Id, StudentId = student.Id });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (student.Id, null, false);
    }
}

public static class StudentEndpoints {
    public static void MapStudents(this WebApplication app) {
        var api = app.MapGroup("/api").RequireAuthorization("Staff");

        // The school directory exposes enrollment only; guardian contacts remain in authorized class records.
        api.MapGet("/student-directory", async (AppDb db, string? search, Guid? gradeId, string? status, int? page) => {
            var query = db.Students.AsNoTracking();
            if (status == "archived") query = query.Where(s => s.IsDeleted);
            else if (status == "unassigned") query = query.Where(s => !s.IsDeleted && s.SectionId == null);
            else if (status == "enrolled") query = query.Where(s => !s.IsDeleted && s.SectionId != null);
            else if (status != "all") query = query.Where(s => !s.IsDeleted);
            if (gradeId.HasValue) query = query.Where(s => s.Section.GradeLevelId == gradeId);
            query = Search(query, search);
            var total = await query.CountAsync();
            var currentPage = Math.Clamp(page ?? 1, 1, Math.Max(1, (int)Math.Ceiling(total / 20.0)));
            var items = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Id)
                .Skip((currentPage - 1) * 20).Take(20).Select(s => new {
                    s.Id, s.StudentNumber, s.FirstName, s.MiddleName, s.LastName, s.Suffix, s.SectionId, s.IsDeleted,
                    gradeId = s.SectionId == null ? (Guid?)null : s.Section.GradeLevelId,
                    grade = s.SectionId == null ? null : s.Section.GradeLevel.Name,
                    section = s.SectionId == null ? null : s.Section.Name,
                    status = s.IsDeleted ? "Archived" : s.SectionId == null ? "Unassigned" :
                        s.Section.IsArchived || s.Section.GradeLevel.IsArchived ? "Section archived" : "Enrolled"
                }).ToListAsync();
            return Results.Ok(new { items, total, page = currentPage });
        });

        api.MapGet("/students", async (AppDb db, ClaimsPrincipal user, string? search, Guid? sectionId, int? page, bool? archived) => {
            var admin = user.IsInRole("Administrator");
            var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var query = db.Students.Where(s => admin && archived == true ? s.IsDeleted : !s.IsDeleted);
            if (!admin) query = query.Where(s => s.Section.AdviserId == id || db.Schedules.Any(sc => sc.SectionId == s.SectionId && sc.TeacherId == id && !sc.IsArchived));
            if (sectionId.HasValue) query = query.Where(s => s.SectionId == sectionId);
            query = Search(query, search);
            var total = await query.CountAsync();
            var currentPage = Math.Clamp(page ?? 1, 1, Math.Max(1, (int)Math.Ceiling(total / 20.0)));
            var items = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Id)
                .Skip((currentPage - 1) * 20).Take(20).Select(s => new {
                    s.Id, s.StudentNumber, s.FirstName, s.MiddleName, s.LastName, s.Suffix, s.SectionId, s.IsDeleted,
                    section = s.SectionId == null ? null : s.Section.Name, grade = s.SectionId == null ? null : s.Section.GradeLevel.Name,
                    guardians = s.Guardians.Select(g => new { g.Guardian.FullName, g.Guardian.Email })
                }).ToListAsync();
            return Results.Ok(new { items, total, page = currentPage });
        });

        api.MapPost("/students", async (StudentInput x, AppDb db, UserManager<AppUser> users, ClaimsPrincipal user) => {
            var result = await StudentRegistry.Create(x, db, users, user);
            if (result.Id.HasValue) return Results.Created($"/api/students/{result.Id}", new { id = result.Id });
            return result.Conflict ? Results.Conflict(new { message = result.Error }) : Results.BadRequest(new { message = result.Error });
        });

        api.MapPost("/students/{id:guid}/enrollment", async (Guid id, EnrollmentInput x, AppDb db, ClaimsPrincipal user) => {
            if (!await StudentRegistry.CanEnrollIn(db, user, x.SectionId)) return Results.BadRequest(new { message = "Choose an active section assigned to you." });
            await using var transaction = await StudentRegistry.BeginWrite(db);
            var student = await db.Students.Include(s => s.Section).ThenInclude(s => s.GradeLevel).FirstOrDefaultAsync(s => s.Id == id);
            if (student == null) return Results.NotFound();
            if (student.IsDeleted) return Results.Conflict(new { message = "Restore this student before enrolling them." });
            if (student.SectionId == x.SectionId) return Results.Ok(new { student.Id });
            if (student.SectionId != x.ExpectedSectionId) return Results.Conflict(new { message = "Enrollment changed. Refresh the Student List and try again." });
            if (student.SectionId != null && (!user.IsInRole("Administrator") || !x.ConfirmTransfer))
                return Results.Conflict(new { message = $"Already enrolled in {student.Section.GradeLevel.Name} – {student.Section.Name}. An administrator can change enrollment from Student List." });
            student.SectionId = x.SectionId;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.Ok(new { student.Id });
        });

        api.MapPut("/students/{id:guid}", async (Guid id, StudentInput x, AppDb db, UserManager<AppUser> users) => {
            if (!StudentRegistry.Valid(x)) return Results.BadRequest(new { message = "Enter valid student and guardian details." });
            await using var transaction = await StudentRegistry.BeginWrite(db);
            var student = await db.Students.Include(s => s.Guardians).FirstOrDefaultAsync(s => s.Id == id);
            if (student == null || student.IsDeleted) return Results.NotFound();
            if (student.SectionId != x.SectionId) return Results.Conflict(new { message = "Change enrollment from Student List." });
            var duplicate = await StudentRegistry.FindDuplicate(db, x.FirstName, x.LastName, x.GuardianEmail, id);
            if (duplicate != null) return Results.Conflict(new { message = StudentRegistry.DuplicateMessage(duplicate) });
            var (guardian, error) = await StudentRegistry.Guardian(users, x.GuardianName, x.GuardianEmail);
            if (guardian == null) return Results.BadRequest(new { message = error });
            student.FirstName = StudentRegistry.Clean(x.FirstName); student.MiddleName = StudentRegistry.Clean(x.MiddleName);
            student.LastName = StudentRegistry.Clean(x.LastName); student.Suffix = StudentRegistry.Clean(x.Suffix);
            if (!student.Guardians.Any(g => g.GuardianId == guardian.Id)) {
                db.GuardianStudents.RemoveRange(student.Guardians);
                db.GuardianStudents.Add(new GuardianStudent { StudentId = id, GuardianId = guardian.Id });
            }
            await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Results.Ok(new { student.Id });
        }).RequireAuthorization("Administrator");

        api.MapDelete("/students/{id:guid}", async (Guid id, AppDb db) => {
            await using var transaction = await StudentRegistry.BeginWrite(db);
            var student = await db.Students.FindAsync(id); if (student == null) return Results.NotFound();
            student.IsDeleted = true; await db.SaveChangesAsync(); await transaction.CommitAsync(); return Results.NoContent();
        }).RequireAuthorization("Administrator");
        api.MapPost("/students/{id:guid}/restore", async (Guid id, AppDb db) => {
            await using var transaction = await StudentRegistry.BeginWrite(db);
            var student = await db.Students.FindAsync(id); if (student == null) return Results.NotFound();
            student.IsDeleted = false; await db.SaveChangesAsync(); await transaction.CommitAsync(); return Results.NoContent();
        }).RequireAuthorization("Administrator");
    }

    private static IQueryable<Student> Search(IQueryable<Student> query, string? search) {
        if (string.IsNullOrWhiteSpace(search)) return query;
        foreach (var part in StudentRegistry.Clean(search).ToLowerInvariant().Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            var term = part;
            query = query.Where(s => (s.FirstName + " " + (s.MiddleName ?? "") + " " + s.LastName + " " + (s.Suffix ?? "") + " " + s.StudentNumber).ToLower().Contains(term));
        }
        return query;
    }
}
