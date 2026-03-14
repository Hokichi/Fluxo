using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fluxo.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppSettings",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AppSettings", x => x.Key));

        migrationBuilder.CreateTable(
            name: "BnplSources",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                CreditLimit = table.Column<decimal>(type: "REAL", nullable: true),
                CurrentBalance = table.Column<decimal>(type: "REAL", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BnplSources", x => x.Id));

        migrationBuilder.CreateTable(
            name: "BudgetConfigs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                Year = table.Column<int>(type: "INTEGER", nullable: false),
                NeedsPercentage = table.Column<decimal>(type: "REAL", nullable: false),
                WantsPercentage = table.Column<decimal>(type: "REAL", nullable: false),
                SavingsPercentage = table.Column<decimal>(type: "REAL", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BudgetConfigs", x => x.Id));

        migrationBuilder.CreateTable(
            name: "IncomeSources",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_IncomeSources", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SavingsAccounts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                InitialBalance = table.Column<decimal>(type: "REAL", nullable: false),
                CurrentBalance = table.Column<decimal>(type: "REAL", nullable: false),
                AnnualInterestRate = table.Column<decimal>(type: "REAL", nullable: false),
                StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SavingsAccounts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SavingsGoals",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                TargetAmount = table.Column<decimal>(type: "REAL", nullable: false),
                CurrentAmount = table.Column<decimal>(type: "REAL", nullable: false),
                ContributionAmount = table.Column<decimal>(type: "REAL", nullable: false),
                ContributionFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsManualDate = table.Column<bool>(type: "INTEGER", nullable: false),
                EstimatedCompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SavingsGoals", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Tags",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Color = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Tags", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Expenses",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Amount = table.Column<decimal>(type: "REAL", nullable: false),
                Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsManualDate = table.Column<bool>(type: "INTEGER", nullable: false),
                Category = table.Column<int>(type: "INTEGER", nullable: false),
                IsBnpl = table.Column<bool>(type: "INTEGER", nullable: false),
                BnplSourceId = table.Column<int>(type: "INTEGER", nullable: true),
                BnplSetAsideAmount = table.Column<decimal>(type: "REAL", nullable: true),
                BnplInstallmentCount = table.Column<int>(type: "INTEGER", nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Expenses", x => x.Id);
                table.ForeignKey(
                    name: "FK_Expenses_BnplSources_BnplSourceId",
                    column: x => x.BnplSourceId,
                    principalTable: "BnplSources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "FixedExpenses",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                AmountMode = table.Column<int>(type: "INTEGER", nullable: false),
                Amount = table.Column<decimal>(type: "REAL", nullable: true),
                DueDay = table.Column<int>(type: "INTEGER", nullable: false),
                Category = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                NotificationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                LastPaidDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_FixedExpenses", x => x.Id));

        migrationBuilder.CreateTable(
            name: "IncomeEntries",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                IncomeSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                Amount = table.Column<decimal>(type: "REAL", nullable: false),
                Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsManualDate = table.Column<bool>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncomeEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_IncomeEntries_IncomeSources_IncomeSourceId",
                    column: x => x.IncomeSourceId,
                    principalTable: "IncomeSources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ExpenseTags",
            columns: table => new
            {
                ExpenseId = table.Column<int>(type: "INTEGER", nullable: false),
                TagId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseTags", x => new { x.ExpenseId, x.TagId });
                table.ForeignKey(
                    name: "FK_ExpenseTags_Expenses_ExpenseId",
                    column: x => x.ExpenseId,
                    principalTable: "Expenses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ExpenseTags_Tags_TagId",
                    column: x => x.TagId,
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FixedExpenseTags",
            columns: table => new
            {
                FixedExpenseId = table.Column<int>(type: "INTEGER", nullable: false),
                TagId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FixedExpenseTags", x => new { x.FixedExpenseId, x.TagId });
                table.ForeignKey(
                    name: "FK_FixedExpenseTags_FixedExpenses_FixedExpenseId",
                    column: x => x.FixedExpenseId,
                    principalTable: "FixedExpenses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_FixedExpenseTags_Tags_TagId",
                    column: x => x.TagId,
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FixedExpenseHistory",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                FixedExpenseId = table.Column<int>(type: "INTEGER", nullable: false),
                Amount = table.Column<decimal>(type: "REAL", nullable: false),
                PaidDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FixedExpenseHistory", x => x.Id);
                table.ForeignKey(
                    name: "FK_FixedExpenseHistory_FixedExpenses_FixedExpenseId",
                    column: x => x.FixedExpenseId,
                    principalTable: "FixedExpenses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Indexes
        migrationBuilder.CreateIndex(name: "IX_BudgetConfigs_Month_Year", table: "BudgetConfigs", columns: new[] { "Month", "Year" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Expenses_BnplSourceId", table: "Expenses", column: "BnplSourceId");
        migrationBuilder.CreateIndex(name: "IX_Expenses_Date", table: "Expenses", column: "Date");
        migrationBuilder.CreateIndex(name: "IX_ExpenseTags_TagId", table: "ExpenseTags", column: "TagId");
        migrationBuilder.CreateIndex(name: "IX_FixedExpenseHistory_FixedExpenseId", table: "FixedExpenseHistory", column: "FixedExpenseId");
        migrationBuilder.CreateIndex(name: "IX_FixedExpenseTags_TagId", table: "FixedExpenseTags", column: "TagId");
        migrationBuilder.CreateIndex(name: "IX_IncomeEntries_IncomeSourceId", table: "IncomeEntries", column: "IncomeSourceId");
        migrationBuilder.CreateIndex(name: "IX_IncomeEntries_Date", table: "IncomeEntries", column: "Date");

        // Seed
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        migrationBuilder.InsertData(
            table: "AppSettings",
            columns: new[] { "Key", "Value", "UpdatedAt" },
            values: new object[,]
            {
                { "currency", "USD", seedDate },
                { "notification_lead_days", "3", seedDate },
                { "default_entry_day", "1", seedDate },
                { "theme", "system", seedDate }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ExpenseTags");
        migrationBuilder.DropTable(name: "FixedExpenseTags");
        migrationBuilder.DropTable(name: "FixedExpenseHistory");
        migrationBuilder.DropTable(name: "Expenses");
        migrationBuilder.DropTable(name: "FixedExpenses");
        migrationBuilder.DropTable(name: "IncomeEntries");
        migrationBuilder.DropTable(name: "IncomeSources");
        migrationBuilder.DropTable(name: "BnplSources");
        migrationBuilder.DropTable(name: "SavingsAccounts");
        migrationBuilder.DropTable(name: "SavingsGoals");
        migrationBuilder.DropTable(name: "BudgetConfigs");
        migrationBuilder.DropTable(name: "Tags");
        migrationBuilder.DropTable(name: "AppSettings");
    }
}