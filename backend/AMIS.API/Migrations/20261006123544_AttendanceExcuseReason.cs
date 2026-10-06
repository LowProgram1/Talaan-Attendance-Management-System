using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMIS.API.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceExcuseReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExcuseReason",
                table: "AttendanceSubmissionItem",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExcuseReason",
                table: "Attendance",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcuseReason",
                table: "AttendanceSubmissionItem");

            migrationBuilder.DropColumn(
                name: "ExcuseReason",
                table: "Attendance");
        }
    }
}
