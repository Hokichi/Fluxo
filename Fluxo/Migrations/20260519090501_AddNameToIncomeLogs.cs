using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddNameToIncomeLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "IncomeLogs",
                type: "TEXT",
                nullable: false,
                defaultValue: "Income");

            migrationBuilder.Sql(
                """
                UPDATE IncomeLogs
                SET
                    Name = COALESCE(NULLIF(TRIM(
                        CASE
                            WHEN INSTR(REPLACE(Notes, CHAR(13), ''), CHAR(10)) > 0
                            THEN SUBSTR(REPLACE(Notes, CHAR(13), ''), 1, INSTR(REPLACE(Notes, CHAR(13), ''), CHAR(10)) - 1)
                            ELSE REPLACE(Notes, CHAR(13), '')
                        END
                    ), ''), 'Income'),
                    Notes = TRIM(
                        CASE
                            WHEN INSTR(REPLACE(Notes, CHAR(13), ''), CHAR(10)) > 0
                            THEN SUBSTR(REPLACE(Notes, CHAR(13), ''), INSTR(REPLACE(Notes, CHAR(13), ''), CHAR(10)) + 1)
                            ELSE ''
                        END
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "IncomeLogs");
        }
    }
}
