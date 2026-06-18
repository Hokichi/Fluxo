using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(FluxoDbContext))]
    [Migration("20260520090000_MoveRecurringPeriodToRecurringTransactions")]
    public partial class MoveRecurringPeriodToRecurringTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE "SavingGoals_new" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SavingGoals" PRIMARY KEY AUTOINCREMENT,
                    "CreatedOn" TEXT NOT NULL,
                    "CurrentAmount" NUMERIC NOT NULL,
                    "Name" TEXT NOT NULL,
                    "SavingEndDate" TEXT NULL,
                    "TargetAmount" NUMERIC NOT NULL
                );

                INSERT INTO "SavingGoals_new" (
                    "Id",
                    "CreatedOn",
                    "CurrentAmount",
                    "Name",
                    "SavingEndDate",
                    "TargetAmount")
                SELECT
                    "Id",
                    "CreatedOn",
                    "CurrentAmount",
                    "Name",
                    "SavingEndDate",
                    "TargetAmount"
                FROM "SavingGoals";

                DROP TABLE "SavingGoals";
                ALTER TABLE "SavingGoals_new" RENAME TO "SavingGoals";
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "RecurringTransactions" RENAME COLUMN "RecurringDate" TO "RecurringTime";
                ALTER TABLE "RecurringTransactions" ADD COLUMN "RecurringPeriod" INTEGER NOT NULL DEFAULT 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE "RecurringTransactions_old" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_RecurringTransactions" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "Amount" NUMERIC NOT NULL,
                    "RecurringDate" INTEGER NOT NULL,
                    "Type" INTEGER NOT NULL,
                    "SourceId" INTEGER NOT NULL,
                    "TagId" INTEGER NULL,
                    "GoalId" INTEGER NULL,
                    "IsEnabled" INTEGER NOT NULL,
                    CONSTRAINT "FK_RecurringTransactions_ExpenseTags_TagId" FOREIGN KEY ("TagId") REFERENCES "ExpenseTags" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_RecurringTransactions_SavingGoals_GoalId" FOREIGN KEY ("GoalId") REFERENCES "SavingGoals" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_RecurringTransactions_Accounts_SourceId" FOREIGN KEY ("SourceId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT
                );

                INSERT INTO "RecurringTransactions_old" (
                    "Id",
                    "Name",
                    "Amount",
                    "RecurringDate",
                    "Type",
                    "SourceId",
                    "TagId",
                    "GoalId",
                    "IsEnabled")
                SELECT
                    "Id",
                    "Name",
                    "Amount",
                    "RecurringTime",
                    "Type",
                    "SourceId",
                    "TagId",
                    "GoalId",
                    "IsEnabled"
                FROM "RecurringTransactions";

                DROP TABLE "RecurringTransactions";
                ALTER TABLE "RecurringTransactions_old" RENAME TO "RecurringTransactions";
                CREATE INDEX "IX_RecurringTransactions_GoalId" ON "RecurringTransactions" ("GoalId");
                CREATE INDEX "IX_RecurringTransactions_SourceId" ON "RecurringTransactions" ("SourceId");
                CREATE INDEX "IX_RecurringTransactions_TagId" ON "RecurringTransactions" ("TagId");

                ALTER TABLE "SavingGoals" ADD COLUMN "RecurringPeriod" INTEGER NOT NULL DEFAULT 0;
                """);
        }
    }
}
