using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AMIS.API;

public sealed record CsvRequest(string Csv);
public sealed record ImportRow(int Line, string FirstName, string LastName, Guid? SectionId, string GuardianName, string GuardianEmail, string[] Errors);

public static class ImportEndpoints {
    private static readonly string[] Columns = ["firstName", "lastName", "sectionId", "guardianName", "guardianEmail"];

    public static void MapImport(this WebApplication app) {
        var api = app.MapGroup("/api/students/import").RequireAuthorization("Staff");
        api.MapPost("/preview", async (CsvRequest input, AppDb db, ClaimsPrincipal principal) =>
            Results.Ok(await Preview(input.Csv, db, principal.IsInRole("Administrator") ? null : Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        api.MapPost("/commit", async (CsvRequest input, AppDb db, UserManager<AppUser> users, ClaimsPrincipal principal) => {
            var rows = await Preview(input.Csv, db, principal.IsInRole("Administrator") ? null : Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!));
            var imported = 0;
            var failed = new List<object>();
            foreach (var row in rows.Where(x => x.Errors.Length == 0)) {
                try {
                    var guardian = await users.FindByEmailAsync(row.GuardianEmail);
                    if (guardian is null) {
                        guardian = new AppUser { UserName = row.GuardianEmail, Email = row.GuardianEmail, FullName = row.GuardianName, IsActive = false };
                        var created = await users.CreateAsync(guardian);
                        if (!created.Succeeded) { failed.Add(new { row.Line, error = string.Join(" ", created.Errors.Select(e => e.Description)) }); continue; }
                        await users.AddToRoleAsync(guardian, "Guardian");
                    } else if (!await users.IsInRoleAsync(guardian, "Guardian")) {
                        failed.Add(new { row.Line, error = "Guardian email belongs to another role." });
                        continue;
                    }
                    var student = new Student { FirstName = row.FirstName, LastName = row.LastName, SectionId = row.SectionId!.Value };
                    student.StudentNumber = await SchoolEndpoints.NextStudentNumber(db);
                    db.Students.Add(student);
                    db.GuardianStudents.Add(new GuardianStudent { StudentId = student.Id, GuardianId = guardian.Id });
                    await db.SaveChangesAsync();
                    imported++;
                } catch { failed.Add(new { row.Line, error = "Unable to import row." }); }
            }
            return Results.Ok(new { imported, invalid = rows.Count(x => x.Errors.Length > 0), failed });
        });
    }

    public static async Task<List<ImportRow>> Preview(string csv, AppDb db, Guid? teacherId = null) {
        static ImportRow Error(string message) => new(0, "", "", null, "", "", [message]);
        if (csv.Length > 1_000_000) return [Error("File exceeds 1 MB.")];
        var lines = csv.Replace("\r\n", "\n").Trim().Split('\n');
        if (lines.Length < 2) return [Error("CSV must contain a header and at least one row.")];
        var headers = Parse(lines[0]).Select(x => x.Trim().TrimStart('\uFEFF')).ToArray();
        if (!Columns.SequenceEqual(headers, StringComparer.OrdinalIgnoreCase)) return [Error("Required columns: " + string.Join(", ", Columns))];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ImportRow>();
        for (var i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var c = Parse(lines[i]);
            if (c.Count != 5) { result.Add(new ImportRow(i + 1, "", "", null, "", "", ["Expected five columns."])); continue; }
            var first = c[0].Trim();
            var last = c[1].Trim();
            var guardianName = c[3].Trim();
            var guardianEmail = c[4].Trim();
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(guardianName)) errors.Add("Required value is missing.");
            if (!Guid.TryParse(c[2], out var sectionId) || !await db.Sections.AnyAsync(s => s.Id == sectionId && !s.IsArchived && !s.GradeLevel.IsArchived && (teacherId == null || s.AdviserId == teacherId || db.Schedules.Any(sc => sc.SectionId == s.Id && sc.TeacherId == teacherId && !sc.IsArchived)))) errors.Add("Unknown, archived, or unassigned section.");
            if (!new EmailAddressAttribute().IsValid(guardianEmail)) errors.Add("Invalid guardian email.");
            if (first.Length > 0 && last.Length > 0 && sectionId != default && guardianEmail.Length > 0) {
                var key = $"{first}|{last}|{sectionId}|{guardianEmail}";
                if (!seen.Add(key) || await db.Students.AnyAsync(s => !s.IsDeleted && s.SectionId == sectionId && s.FirstName.ToLower() == first.ToLower() && s.LastName.ToLower() == last.ToLower() && s.Guardians.Any(g => g.Guardian.Email == guardianEmail))) errors.Add("Duplicate student in this section.");
            }
            result.Add(new ImportRow(i + 1, first, last, sectionId == default ? null : sectionId, guardianName, guardianEmail, errors.ToArray()));
        }
        return result;
    }

    private static List<string> Parse(string line) {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++) {
            var c = line[i];
            if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }
}
