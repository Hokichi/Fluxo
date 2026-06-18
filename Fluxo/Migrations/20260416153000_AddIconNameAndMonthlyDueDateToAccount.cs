using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddIconNameAndMonthlyDueDateToAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconName",
                table: "ExpenseTags",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MonthlyDueDate",
                table: "Accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "Accounts"
                SET "MonthlyDueDate" = CASE
                    WHEN "DueDate" IS NULL THEN 0
                    ELSE CAST(strftime('%d', "DueDate") AS INTEGER)
                END;
                """);

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
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Accounts"
                SET "DueDate" = CASE
                    WHEN "MonthlyDueDate" <= 0 THEN NULL
                    ELSE date(
                        date('now', 'start of month', '+1 month', '-1 day'),
                        '-' || (CAST(strftime('%d', date('now', 'start of month', '+1 month', '-1 day')) AS INTEGER) - MIN("MonthlyDueDate", CAST(strftime('%d', date('now', 'start of month', '+1 month', '-1 day')) AS INTEGER))) || ' days'
                    )
                END;
                """);

            migrationBuilder.DropColumn(
                name: "IconName",
                table: "ExpenseTags");

            migrationBuilder.Sql("""
                PRAGMA foreign_keys=OFF;

                CREATE TABLE "__tmp_Accounts" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "AccountType" INTEGER NOT NULL,
                    "Balance" TEXT NOT NULL,
                    "DueDate" TEXT NULL,
                    "InterestRate" TEXT NULL,
                    "AccountLimit" TEXT NOT NULL DEFAULT '0.0',
                    "SpentAmount" TEXT NOT NULL DEFAULT '0.0',
                    "ShowOnUI" INTEGER NOT NULL DEFAULT 0,
                    "IsEnabled" INTEGER NOT NULL DEFAULT 0,
                    "IsForDeletion" INTEGER NOT NULL DEFAULT 0
                );

                INSERT INTO "__tmp_Accounts"
                    ("Id", "Name", "AccountType", "Balance", "DueDate", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion")
                SELECT
                    "Id", "Name", "AccountType", "Balance", "DueDate", "InterestRate", "AccountLimit", "SpentAmount", "ShowOnUI", "IsEnabled", "IsForDeletion"
                FROM "Accounts";

                DROP TABLE "Accounts";
                ALTER TABLE "__tmp_Accounts" RENAME TO "Accounts";

                PRAGMA foreign_keys=ON;
                """);
        }
    }
}
