using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMIS.API.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeLegacyStudentIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SELECT setval('student_number_sequence',
                    GREATEST(
                        (SELECT last_value FROM student_number_sequence),
                        COALESCE((SELECT MAX(substring("StudentNumber" FROM 5)::bigint)
                                  FROM "Students"
                                  WHERE "StudentNumber" ~ '^STD-[0-9]{7,}$'), 0)),
                    true);

                WITH normalized AS (
                    UPDATE "Students"
                    SET "StudentNumber" = 'STD-' || lpad(nextval('student_number_sequence')::text, 7, '0')
                    WHERE "StudentNumber" !~ '^STD-[0-9]{7,}$'
                    RETURNING "Id", "StudentNumber"
                )
                UPDATE "AttendanceSubmissionItem" item
                SET "StudentNumber" = normalized."StudentNumber"
                FROM normalized
                WHERE item."StudentId" = normalized."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
