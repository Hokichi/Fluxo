using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class SeedBudgetReconciliationTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO ExpenseTags (Name, HexCode, IsSystemTag)
                SELECT 'Budget Reconciliation', '#f0ebbe', 1
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM ExpenseTags
                    WHERE Name = 'Budget Reconciliation'
                      AND IsSystemTag = 1
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM ExpenseTags AS tag
                WHERE tag.Name = 'Budget Reconciliation'
                  AND tag.IsSystemTag = 1
                  AND NOT EXISTS (
                      SELECT 1
                      FROM Expenses AS expense
                      WHERE expense.ExpenseTagId = tag.Id
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM RecurringTransactions AS transaction
                      WHERE transaction.TagId = tag.Id
                  );
                """);
        }
    }
}
