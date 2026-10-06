using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMIS.API.Migrations
{
    /// <inheritdoc />
    public partial class StudentNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SEQUENCE student_number_sequence START WITH 1 MINVALUE 1 MAXVALUE 9999999");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE student_number_sequence");
        }
    }
}
