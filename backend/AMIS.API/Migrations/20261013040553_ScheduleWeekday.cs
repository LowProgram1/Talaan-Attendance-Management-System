using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMIS.API.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleWeekday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DayOfWeek",
                table: "Schedules",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "Schedules");
        }
    }
}
