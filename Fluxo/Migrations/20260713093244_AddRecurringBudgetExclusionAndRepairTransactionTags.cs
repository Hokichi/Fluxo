using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringBudgetExclusionAndRepairTransactionTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExcludedFromBudget",
                table: "RecurringTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                INSERT INTO "Tags" ("Name", "HexCode", "IsSystemTag")
                SELECT 'Balance Update', '#a3e5d6', 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Tags"
                    WHERE "Name" = 'Balance Update' AND "IsSystemTag" = 1);

                UPDATE "Transactions"
                SET "TagId" = (
                    SELECT "Id" FROM "Tags"
                    WHERE "Name" = 'Balance Update' AND "IsSystemTag" = 1
                    ORDER BY "Id" LIMIT 1)
                WHERE "Type" = 1
                    AND "RepaymentAccountId" IS NOT NULL
                    AND "SourceAccountId" = "RepaymentAccountId"
                    AND "Name" LIKE 'Repayment from %';

                UPDATE "Transactions"
                SET "TagId" = NULL
                WHERE "Id" IN (
                    SELECT "ParentTransactionId" FROM "Transactions"
                    WHERE "ParentTransactionId" IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExcludedFromBudget",
                table: "RecurringTransactions");
        }
    }
}
