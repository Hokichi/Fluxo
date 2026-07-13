using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class ExcludeIoUAndIncomeFromBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Transactions"
                SET "IsExcludedFromBudget" = 1
                WHERE "Type" = 1 OR "IsIoU" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
