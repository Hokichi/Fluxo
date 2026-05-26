using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class SeedDataRestorationTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO ExpenseTags (Name, HexCode, IsSystemTag)
                SELECT 'Data Restoration', '#e9c178', 1
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM ExpenseTags
                    WHERE Name = 'Data Restoration'
                      AND IsSystemTag = 1
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM ExpenseTags AS tag
                WHERE tag.Name = 'Data Restoration'
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
