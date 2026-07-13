using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class ClearParentTransactionCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Transactions"
                SET "ExpenseCategory" = NULL
                WHERE "Id" IN (
                    SELECT DISTINCT "ParentTransactionId"
                    FROM "Transactions"
                    WHERE "ParentTransactionId" IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
