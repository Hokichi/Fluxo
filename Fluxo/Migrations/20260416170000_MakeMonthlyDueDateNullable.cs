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

                CREATE TABLE "__tmp_Accounts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "AccountType" INTEGER NOT NULL,
                    "Balance" TEXT NOT NULL,
                    "InterestRate" TEXT NULL,
                    "AccountLimit" TEXT NOT NULL DEFAULT '0.0',
                    "SpentAmount" TEXT NOT NULL DEFAULT '0.0',
                    "ShowOnUI" INTEGER NOT NULL DEFAULT 0,
                    "IsEnabled" INTEGER NOT NULL DEFAULT 0,
                    "IsForDeletion" INTEGER NOT NULL DEFAULT 0,
                    "MonthlyDueDate" INTEGER NULL
                );

                INSERT INTO "__tmp_Accounts"
                    ("Id", "Name", "AccountType", "Balance", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion", "MonthlyDueDate")
                SELECT
                    "Id", "Name", "AccountType", "Balance", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion", "MonthlyDueDate"
                FROM "Accounts";

                DROP TABLE "Accounts";
                ALTER TABLE "__tmp_Accounts" RENAME TO "Accounts";

                PRAGMA foreign_keys=ON;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                PRAGMA foreign_keys=OFF;

                CREATE TABLE "__tmp_Accounts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "AccountType" INTEGER NOT NULL,
                    "Balance" TEXT NOT NULL,
                    "InterestRate" TEXT NULL,
                    "AccountLimit" TEXT NOT NULL DEFAULT '0.0',
                    "SpentAmount" TEXT NOT NULL DEFAULT '0.0',
                    "ShowOnUI" INTEGER NOT NULL DEFAULT 0,
                    "IsEnabled" INTEGER NOT NULL DEFAULT 0,
                    "IsForDeletion" INTEGER NOT NULL DEFAULT 0,
                    "MonthlyDueDate" INTEGER NOT NULL DEFAULT 0
                );

                INSERT INTO "__tmp_Accounts"
                    ("Id", "Name", "AccountType", "Balance", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion", "MonthlyDueDate")
                SELECT
                    "Id", "Name", "AccountType", "Balance", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion", COALESCE("MonthlyDueDate", 0)
                FROM "Accounts";

                DROP TABLE "Accounts";
                ALTER TABLE "__tmp_Accounts" RENAME TO "Accounts";

                PRAGMA foreign_keys=ON;
                """);
        }
    }
}
