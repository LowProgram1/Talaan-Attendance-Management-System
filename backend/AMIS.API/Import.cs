using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AMIS.API;

public sealed record CsvRequest(string Csv, Guid? SectionId = null);
public sealed record ImportRow(int Line, string FirstName, string LastName, Guid? SectionId, string GuardianName, string GuardianEmail, string[] Errors);

public static class ImportEndpoints {
    private static readonly string[] Columns = ["firstName", "lastName", "guardianName", "guardianEmail"];

    public static void MapImport(this WebApplication app) {
        var api = app.MapGroup("/api/students/import").RequireAuthorization("Staff");
        api.MapPost("/preview", async (CsvRequest input, AppDb db, ClaimsPrincipal principal) =>
            Results.Ok(await Preview(input.Csv, db, principal.IsInRole("Administrator") ? null : Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), input.SectionId)));
        api.MapPost("/commit", async (CsvRequest input, AppDb db, UserManager<AppUser> users, ClaimsPrincipal principal) => {
            var rows = await Preview(input.Csv, db, principal.IsInRole("Administrator") ? null : Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), input.SectionId);
            var imported = 0;
            var failed = new List<object>();
            foreach (var row in rows.Where(x => x.Errors.Length == 0)) {
                try {
                    var result = await StudentRegistry.Create(new StudentInput(row.FirstName, null, row.LastName, null,
                        row.SectionId, row.GuardianName, row.GuardianEmail), db, users, principal);
                    if (result.Id.HasValue) imported++;
                    else failed.Add(new { row.Line, error = result.Error });
                } catch { failed.Add(new { row.Line, error = "Unable to import row." }); }
                finally { db.ChangeTracker.Clear(); }
            }
            return Results.Ok(new { imported, invalid = rows.Count(x => x.Errors.Length > 0), failed });
        });
    }

    public static async Task<List<ImportRow>> Preview(string csv, AppDb db, Guid? teacherId = null, Guid? targetSectionId = null) {
        static ImportRow Error(string message) => new(0, "", "", null, "", "", [message]);
        if (csv.Length > 1_000_000) return [Error("File exceeds 1 MB.")];
        var lines = csv.Replace("\r\n", "\n").Trim().Split('\n');
        if (lines.Length < 2) return [Error("CSV must contain a header and at least one row.")];
        var headers = Parse(lines[0]).Select(x => x.Trim().TrimStart('\uFEFF')).ToArray();
        if (!Columns.SequenceEqual(headers, StringComparer.OrdinalIgnoreCase)) return [Error("Required columns: " + string.Join(", ", Columns))];
        if (!targetSectionId.HasValue || targetSectionId.Value == Guid.Empty)
            return [Error("Select a destination section before importing.")];
        var sectionId = targetSectionId.Value;
        if (!await db.Sections.AnyAsync(s => s.Id == sectionId && !s.IsArchived && !s.GradeLevel.IsArchived && (teacherId == null || s.AdviserId == teacherId || db.Schedules.Any(sc => sc.SectionId == s.Id && sc.TeacherId == teacherId && !sc.IsArchived))))
            return [Error("Unknown, archived, or unassigned destination section.")];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ImportRow>();
        for (var i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var c = Parse(lines[i]);
            if (c.Count != 4) { result.Add(new ImportRow(i + 1, "", "", null, "", "", ["Expected four columns."])); continue; }
            var first = StudentRegistry.Clean(c[0]);
            var last = StudentRegistry.Clean(c[1]);
            var guardianName = c[2].Trim();
            var guardianEmail = c[3].Trim();
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(guardianName)) errors.Add("Required value is missing.");
            if (first.Length > 100 || last.Length > 100 || guardianName.Length > 100) errors.Add("Names must be 100 characters or fewer.");
            if (!new EmailAddressAttribute().IsValid(guardianEmail)) errors.Add("Invalid guardian email.");
            if (first.Length > 0 && last.Length > 0 && sectionId != default && guardianEmail.Length > 0) {
                var key = StudentRegistry.IdentityKey(first, last, guardianEmail);
                if (!seen.Add(key) || await StudentRegistry.FindDuplicate(db, first, last, guardianEmail) != null)
                    errors.Add("Student already exists in the school directory. Use Student List to manage enrollment.");
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
