using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMIS.API.Migrations
{
    /// <inheritdoc />
    public partial class AcademicArchiveAttendanceSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Sections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "GradeLevels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AttendanceSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectionReason = table.Column<string>(type: "text", nullable: true),
                    PresentCount = table.Column<int>(type: "integer", nullable: false),
                    AbsentCount = table.Column<int>(type: "integer", nullable: false),
                    LateCount = table.Column<int>(type: "integer", nullable: false),
                    ExcusedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSubmissions_AspNetUsers_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceSubmissions_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSubmissionItem",
                columns: table => new
                {
                    AttendanceSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentNumber = table.Column<string>(type: "text", nullable: false),
                    StudentName = table.Column<string>(type: "text", nullable: false),
                    GuardianNames = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSubmissionItem", x => new { x.AttendanceSubmissionId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_AttendanceSubmissionItem_AttendanceSubmissions_AttendanceSu~",
                        column: x => x.AttendanceSubmissionId,
                        principalTable: "AttendanceSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmissions_IdempotencyKey",
                table: "AttendanceSubmissions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmissions_ScheduleId_Date",
                table: "AttendanceSubmissions",
                columns: new[] { "ScheduleId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmissions_ScheduleId_Date_Revision",
                table: "AttendanceSubmissions",
                columns: new[] { "ScheduleId", "Date", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSubmissions_SubmittedById",
                table: "AttendanceSubmissions",
                column: "SubmittedById");

            // Existing attendance remains visible as the first recorded submission
            // for its class and date. Later saves create immutable revisions.
            migrationBuilder.Sql("""
                INSERT INTO "AttendanceSubmissions"
                    ("Id", "ScheduleId", "Date", "SubmittedById", "SubmittedAt", "Revision",
                     "IdempotencyKey", "PresentCount", "AbsentCount", "LateCount", "ExcusedCount")
                SELECT gen_random_uuid(), a."ScheduleId", a."Date",
                       (array_agg(a."RecordedById" ORDER BY a."UpdatedAt" DESC))[1],
                       max(a."UpdatedAt"), 1, gen_random_uuid(),
                       count(*) FILTER (WHERE a."Status" = 'Present'),
                       count(*) FILTER (WHERE a."Status" = 'Absent'),
                       count(*) FILTER (WHERE a."Status" = 'Late'),
                       count(*) FILTER (WHERE a."Status" = 'Excused')
                FROM "Attendance" a
                GROUP BY a."ScheduleId", a."Date";

                INSERT INTO "AttendanceSubmissionItem"
                    ("AttendanceSubmissionId", "StudentId", "StudentNumber", "StudentName",
                     "GuardianNames", "Status")
                SELECT sub."Id", a."StudentId", student."StudentNumber",
                       concat_ws(' ', student."FirstName", student."LastName"),
                       coalesce(guardians."Names", ''), a."Status"
                FROM "AttendanceSubmissions" sub
                JOIN "Attendance" a
                  ON a."ScheduleId" = sub."ScheduleId" AND a."Date" = sub."Date"
                JOIN "Students" student ON student."Id" = a."StudentId"
                LEFT JOIN (
                    SELECT relation."StudentId",
                           string_agg(account."FullName", ', ' ORDER BY account."FullName") AS "Names"
                    FROM "GuardianStudents" relation
                    JOIN "AspNetUsers" account ON account."Id" = relation."GuardianId"
                    GROUP BY relation."StudentId"
                ) guardians ON guardians."StudentId" = a."StudentId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceSubmissionItem");

            migrationBuilder.DropTable(
                name: "AttendanceSubmissions");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "GradeLevels");
        }
    }
}
