using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class EnsureSystemTagsAfterTransactionMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO Tags (Name, HexCode, IsSystemTag)
                SELECT 'Data Restoration', '#e9c178', 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM Tags
                    WHERE Name = 'Data Restoration' AND IsSystemTag = 1
                );

                INSERT INTO Tags (Name, HexCode, IsSystemTag)
                SELECT 'Budget Reconciliation', '#f0ebbe', 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM Tags
                    WHERE Name = 'Budget Reconciliation' AND IsSystemTag = 1
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // System tags may now be referenced by transactions. Keep data on downgrade.
        }
    }
}
