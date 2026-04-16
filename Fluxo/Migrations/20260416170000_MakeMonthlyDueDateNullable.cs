using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class MakeMonthlyDueDateNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                PRAGMA foreign_keys=OFF;

                CREATE TABLE "__tmp_SpendingSources" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SpendingSources" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "SpendingSourceType" INTEGER NOT NULL,
                    "Balance" TEXT NOT NULL,
                    "InterestRate" TEXT NULL,
                    "AccountLimit" TEXT NOT NULL DEFAULT '0.0',
                    "SpentAmount" TEXT NOT NULL DEFAULT '0.0',
                    "ShowOnUI" INTEGER NOT NULL DEFAULT 0,
                    "IsEnabled" INTEGER NOT NULL DEFAULT 0,
                    "IsForDeletion" INTEGER NOT NULL DEFAULT 0,
                    "MonthlyDueDate" INTEGER NULL
                );

                INSERT INTO "__tmp_SpendingSources"
                    ("Id", "Name", "SpendingSourceType", "Balance", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion", "MonthlyDueDate")
                SELECT
                    "Id", "Name", "SpendingSourceType", "Balance", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion", "MonthlyDueDate"
                FROM "SpendingSources";

                DROP TABLE "SpendingSources";
                ALTER TABLE "__tmp_SpendingSources" RENAME TO "SpendingSources";

                PRAGMA foreign_keys=ON;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                PRAGMA foreign_keys=OFF;

                CREATE TABLE "__tmp_SpendingSources" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SpendingSources" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "SpendingSourceType" INTEGER NOT NULL,
                    "Balance" TEXT NOT NULL,
                    "InterestRate" TEXT NULL,
                    "AccountLimit" TEXT NOT NULL DEFAULT '0.0',
                    "SpentAmount" TEXT NOT NULL DEFAULT '0.0',
                    "ShowOnUI" INTEGER NOT NULL DEFAULT 0,
                    "IsEnabled" INTEGER NOT NULL DEFAULT 0,
                    "IsForDeletion" INTEGER NOT NULL DEFAULT 0,
                    "MonthlyDueDate" INTEGER NOT NULL DEFAULT 0
                );

                INSERT INTO "__tmp_SpendingSources"
                    ("Id", "Name", "SpendingSourceType", "Balance", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion", "MonthlyDueDate")
                SELECT
                    "Id", "Name", "SpendingSourceType", "Balance", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion", COALESCE("MonthlyDueDate", 0)
                FROM "SpendingSources";

                DROP TABLE "SpendingSources";
                ALTER TABLE "__tmp_SpendingSources" RENAME TO "SpendingSources";

                PRAGMA foreign_keys=ON;
                """);
        }
    }
}
