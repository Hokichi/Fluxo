using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetAllocation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NeedsThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    WantsThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    InvestThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocationPeriod = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocationLimit = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    NeedsDebt = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    WantsDebt = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    InvestDebt = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    RolloverPolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    OverspendPolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    SingletonKey = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetAllocation", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAllocation_SingletonKey",
                table: "BudgetAllocation",
                column: "SingletonKey",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "BudgetAllocation" (
                    "NeedsThreshold",
                    "WantsThreshold",
                    "InvestThreshold",
                    "AllocationPeriod",
                    "AllocationLimit",
                    "NeedsDebt",
                    "WantsDebt",
                    "InvestDebt",
                    "RolloverPolicy",
                    "OverspendPolicy")
                SELECT
                    COALESCE((
                        SELECT CASE
                            WHEN trim("Value") GLOB '[0-9]*'
                             AND trim("Value") NOT GLOB '*[^0-9]*'
                            THEN CAST(trim("Value") AS INTEGER)
                            ELSE NULL
                        END
                        FROM "UserSettings"
                        WHERE "Name" = 'NeedsThreshold'
                        LIMIT 1
                    ), 50),
                    COALESCE((
                        SELECT CASE
                            WHEN trim("Value") GLOB '[0-9]*'
                             AND trim("Value") NOT GLOB '*[^0-9]*'
                            THEN CAST(trim("Value") AS INTEGER)
                            ELSE NULL
                        END
                        FROM "UserSettings"
                        WHERE "Name" = 'WantsThreshold'
                        LIMIT 1
                    ), 30),
                    COALESCE((
                        SELECT CASE
                            WHEN trim("Value") GLOB '[0-9]*'
                             AND trim("Value") NOT GLOB '*[^0-9]*'
                            THEN CAST(trim("Value") AS INTEGER)
                            ELSE NULL
                        END
                        FROM "UserSettings"
                        WHERE "Name" = 'InvestThreshold'
                        LIMIT 1
                    ), 20),
                    COALESCE((
                        SELECT CASE "Value"
                            WHEN 'Weekly' THEN 0
                            WHEN 'Biweekly' THEN 1
                            WHEN 'Monthly' THEN 2
                            WHEN 'Quarterly' THEN 3
                            WHEN 'Yearly' THEN 4
                            ELSE 2
                        END
                        FROM "UserSettings"
                        WHERE "Name" = 'AllocationPeriod'
                        LIMIT 1
                    ), 2),
                    0,
                    0,
                    0,
                    0,
                    0,
                    0
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "BudgetAllocation"
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM "UserSettings"
                WHERE "Name" IN (
                    'NeedsThreshold',
                    'WantsThreshold',
                    'InvestThreshold',
                    'AllocationPeriod'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT OR REPLACE INTO "UserSettings" ("Name", "Value")
                SELECT 'NeedsThreshold', CAST("NeedsThreshold" AS TEXT)
                FROM "BudgetAllocation"
                ORDER BY "Id"
                LIMIT 1;

                INSERT OR REPLACE INTO "UserSettings" ("Name", "Value")
                SELECT 'WantsThreshold', CAST("WantsThreshold" AS TEXT)
                FROM "BudgetAllocation"
                ORDER BY "Id"
                LIMIT 1;

                INSERT OR REPLACE INTO "UserSettings" ("Name", "Value")
                SELECT 'InvestThreshold', CAST("InvestThreshold" AS TEXT)
                FROM "BudgetAllocation"
                ORDER BY "Id"
                LIMIT 1;

                INSERT OR REPLACE INTO "UserSettings" ("Name", "Value")
                SELECT 'AllocationPeriod',
                    CASE "AllocationPeriod"
                        WHEN 0 THEN 'Weekly'
                        WHEN 1 THEN 'Biweekly'
                        WHEN 2 THEN 'Monthly'
                        WHEN 3 THEN 'Quarterly'
                        WHEN 4 THEN 'Yearly'
                        ELSE 'Monthly'
                    END
                FROM "BudgetAllocation"
                ORDER BY "Id"
                LIMIT 1;
                """);

            migrationBuilder.DropTable(
                name: "BudgetAllocation");
        }
    }
}
