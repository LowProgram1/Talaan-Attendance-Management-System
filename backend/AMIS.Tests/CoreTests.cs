using AMIS.API;
using Microsoft.EntityFrameworkCore;

namespace AMIS.Tests;

public class CoreTests {
    [Theory]
    [InlineData(AttendanceStatus.Present)]
    [InlineData(AttendanceStatus.Absent)]
    [InlineData(AttendanceStatus.Late)]
    [InlineData(AttendanceStatus.Excused)]
    public void Attendance_email_names_subject_and_status(AttendanceStatus status) {
        var student = new Student { FirstName = "Mara", LastName = "Reyes" };
        var body = AttendanceEndpoints.NotificationBody(student, "Science", new DateOnly(2026, 10, 6), status,
            status == AttendanceStatus.Excused ? "Medical appointment" : null);
        Assert.Contains("Mara Reyes", body);
        Assert.Contains("Science", body);
        Assert.Contains(status.ToString().ToLowerInvariant(), body);
        if (status == AttendanceStatus.Excused) Assert.Contains("Medical appointment", body);
    }

    [Fact]
    public void Excused_attendance_requires_a_reason_and_encodes_it_in_email() {
        var studentId = Guid.NewGuid();
        Assert.False(AttendanceEndpoints.ValidExcuseReasons([new AttendanceInput(studentId, AttendanceStatus.Excused)]));
        Assert.False(AttendanceEndpoints.ValidExcuseReasons([new AttendanceInput(studentId, AttendanceStatus.Excused, "   ")]));
        Assert.True(AttendanceEndpoints.ValidExcuseReasons([new AttendanceInput(studentId, AttendanceStatus.Excused, "Family appointment")]));
        var body = AttendanceEndpoints.NotificationBody(new Student { FirstName = "Mara", LastName = "Reyes" },
            "Science", new DateOnly(2026, 10, 6), AttendanceStatus.Excused, "<script>unsafe</script>");
        Assert.DoesNotContain("<script>", body);
        Assert.Contains("&lt;script&gt;", body);
    }

    [Theory]
    [InlineData(8, 1, 10, 90)]
    [InlineData(0, 0, 0, null)]
    [InlineData(1, 0, 3, 33)]
    public void Dashboard_attendance_rate_uses_present_and_excused_records(int present, int excused, int total, int? expected) =>
        Assert.Equal(expected, SchoolEndpoints.AttendanceRate(present, excused, total));
    [Theory]
    [InlineData("Administrator", true)]
    [InlineData("Teacher", true)]
    [InlineData("Guardian", false)]
    [InlineData("", false)]
    public void Staff_invitations_allow_only_administrators_and_teachers(string role, bool expected) =>
        Assert.Equal(expected, AuthEndpoints.InvitableStaffRole(role));
    [Theory]
    [InlineData("shortA1!", false)]
    [InlineData("alllowercase123!", false)]
    [InlineData("Password12345!", false)]
    [InlineData("Violet*River482", true)]
    public void Password_policy_rejects_weak_values(string value, bool expected) => Assert.Equal(expected, AuthEndpoints.Strong(value));

    [Fact]
    public void One_time_token_hash_is_salted_and_checks_exact_value() {
        var first = AuthEndpoints.HashToken("314159"); var second = AuthEndpoints.HashToken("314159");
        Assert.NotEqual(first, second);
        Assert.True(AuthEndpoints.VerifyToken(first, "314159"));
        Assert.False(AuthEndpoints.VerifyToken(first, "314158"));
    }

    [Fact]
    public async Task Csv_preview_flags_duplicate_students() {
        await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var grade = new GradeLevel { Name = "Grade 7", Department = "Junior High" }; var section = new Section { Name = "A", GradeLevel = grade };
        db.Sections.Add(section); await db.SaveChangesAsync();
        var csv = $"firstName,lastName,sectionId,guardianName,guardianEmail\nAna,Santos,{section.Id},Maria Santos,maria@example.com\nAna,Santos,{section.Id},Maria Santos,maria@example.com";
        var rows = await ImportEndpoints.Preview(csv, db);
        Assert.Empty(rows[0].Errors);
        Assert.Contains("Duplicate student in this section.", rows[1].Errors);
    }

    [Fact]
    public void Student_number_is_generated_from_the_record_id() {
        Assert.Equal("STD-0000001", SchoolEndpoints.NewStudentNumber(1));
        Assert.Equal("STD-0123456", SchoolEndpoints.NewStudentNumber(123456));
    }

}
