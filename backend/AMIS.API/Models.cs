using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AMIS.API;

public sealed class AppUser : IdentityUser<Guid> { public string FullName { get; set; } = ""; public bool IsActive { get; set; } = true; }
public sealed class GradeLevel { public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = ""; public string Department { get; set; } = ""; public bool IsArchived { get; set; } public List<Section> Sections { get; set; } = []; }
public sealed class Section { public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = ""; public Guid GradeLevelId { get; set; } public GradeLevel GradeLevel { get; set; } = null!; public string Room { get; set; } = ""; public Guid? AdviserId { get; set; } public AppUser? Adviser { get; set; } public bool IsArchived { get; set; } public List<Student> Students { get; set; } = []; }
public sealed class Student { public Guid Id { get; set; } = Guid.NewGuid(); public string StudentNumber { get; set; } = ""; public string FirstName { get; set; } = ""; public string? MiddleName { get; set; } public string LastName { get; set; } = ""; public string? Suffix { get; set; } public DateOnly DateOfBirth { get; set; } public Guid SectionId { get; set; } public Section Section { get; set; } = null!; public bool IsDeleted { get; set; } public List<GuardianStudent> Guardians { get; set; } = []; }
public sealed class GuardianStudent { public Guid GuardianId { get; set; } public AppUser Guardian { get; set; } = null!; public Guid StudentId { get; set; } public Student Student { get; set; } = null!; }
public sealed class Schedule { public Guid Id { get; set; } = Guid.NewGuid(); public Guid SectionId { get; set; } public Section Section { get; set; } = null!; public Guid TeacherId { get; set; } public AppUser Teacher { get; set; } = null!; public string Subject { get; set; } = ""; public TimeOnly StartsAt { get; set; } public TimeOnly EndsAt { get; set; } public bool IsArchived { get; set; } }
public enum AttendanceStatus { Present, Absent, Late, Excused }
public sealed class AttendanceRecord { public Guid Id { get; set; } = Guid.NewGuid(); public Guid StudentId { get; set; } public Student Student { get; set; } = null!; public Guid ScheduleId { get; set; } public Schedule Schedule { get; set; } = null!; public DateOnly Date { get; set; } public AttendanceStatus Status { get; set; } public string? ExcuseReason { get; set; } public Guid RecordedById { get; set; } public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class AttendanceSubmission {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Guid SubmittedById { get; set; }
    public AppUser SubmittedBy { get; set; } = null!;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Revision { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string? CorrectionReason { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LateCount { get; set; }
    public int ExcusedCount { get; set; }
    public List<AttendanceSubmissionItem> Items { get; set; } = [];
}
public sealed class AttendanceSubmissionItem {
    public Guid AttendanceSubmissionId { get; set; }
    public AttendanceSubmission Submission { get; set; } = null!;
    public Guid StudentId { get; set; }
    public string StudentNumber { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string GuardianNames { get; set; } = "";
    public AttendanceStatus Status { get; set; }
    public string? ExcuseReason { get; set; }
}
public sealed class OneTimeToken { public Guid Id { get; set; } = Guid.NewGuid(); public Guid UserId { get; set; } public string Purpose { get; set; } = ""; public string Hash { get; set; } = ""; public DateTimeOffset ExpiresAt { get; set; } public int Attempts { get; set; } public bool Used { get; set; } }
public sealed class AlertLog { public Guid Id { get; set; } = Guid.NewGuid(); public Guid StudentId { get; set; } public string Recipient { get; set; } = ""; public string Status { get; set; } = ""; public bool Delivered { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options) {
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>(); public DbSet<Section> Sections => Set<Section>(); public DbSet<Student> Students => Set<Student>(); public DbSet<GuardianStudent> GuardianStudents => Set<GuardianStudent>(); public DbSet<Schedule> Schedules => Set<Schedule>(); public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>(); public DbSet<AttendanceSubmission> AttendanceSubmissions => Set<AttendanceSubmission>(); public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>(); public DbSet<AlertLog> AlertLogs => Set<AlertLog>();
    protected override void OnModelCreating(ModelBuilder b) {
        base.OnModelCreating(b);
        b.Entity<GradeLevel>().HasIndex(x => x.Name).IsUnique(); b.Entity<Student>().HasIndex(x => x.StudentNumber).IsUnique(); b.Entity<Student>().Property(x => x.DateOfBirth).HasColumnType("date");
        b.Entity<GuardianStudent>().HasKey(x => new { x.GuardianId, x.StudentId }); b.Entity<GuardianStudent>().HasOne(x => x.Student).WithMany(x => x.Guardians).HasForeignKey(x => x.StudentId);
        b.Entity<AttendanceRecord>().HasIndex(x => new { x.StudentId, x.ScheduleId, x.Date }).IsUnique(); b.Entity<AttendanceRecord>().Property(x => x.Date).HasColumnType("date"); b.Entity<AttendanceRecord>().Property(x => x.Status).HasConversion<string>();
        b.Entity<AttendanceSubmission>().HasIndex(x => new { x.ScheduleId, x.Date, x.Revision }).IsUnique();
        b.Entity<AttendanceSubmission>().HasIndex(x => x.IdempotencyKey).IsUnique();
        b.Entity<AttendanceSubmission>().HasIndex(x => new { x.ScheduleId, x.Date });
        b.Entity<AttendanceSubmission>().Property(x => x.Date).HasColumnType("date");
        b.Entity<AttendanceSubmissionItem>().HasKey(x => new { x.AttendanceSubmissionId, x.StudentId });
        b.Entity<AttendanceSubmissionItem>().HasOne(x => x.Submission).WithMany(x => x.Items).HasForeignKey(x => x.AttendanceSubmissionId);
        b.Entity<AttendanceSubmissionItem>().Property(x => x.Status).HasConversion<string>();
        b.Entity<OneTimeToken>().HasIndex(x => new { x.UserId, x.Purpose });
    }
}
