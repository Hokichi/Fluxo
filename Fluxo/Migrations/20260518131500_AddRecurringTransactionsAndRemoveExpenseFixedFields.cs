using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    [DbContext(typeof(FluxoDbContext))]
    [Migration("20260518131500_AddRecurringTransactionsAndRemoveExpenseFixedFields")]
    public partial class AddRecurringTransactionsAndRemoveExpenseFixedFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    RecurringDate = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: true),
                    GoalId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_ExpenseTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ExpenseTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_SavingGoals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "SavingGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringTransactions_SpendingSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "SpendingSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_GoalId",
                table: "RecurringTransactions",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_SourceId",
                table: "RecurringTransactions",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_TagId",
                table: "RecurringTransactions",
                column: "TagId");

            migrationBuilder.Sql(
                """
                INSERT INTO RecurringTransactions (Name, Amount, RecurringDate, Type, SourceId, TagId, GoalId, IsEnabled)
                SELECT
                    Name,
                    Amount,
                    COALESCE(RecurringDate, 1),
                    1,
                    SpendingSourceId,
                    ExpenseTagId,
                    NULL,
                    IsActive
                FROM Expenses
                WHERE ExpenseKind = 1;
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE Expenses_new (
                    Id INTEGER NOT NULL CONSTRAINT PK_Expenses PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Amount NUMERIC NOT NULL,
                    ExpenseCategory INTEGER NOT NULL,
                    ExpenseTagId INTEGER NOT NULL,
                    SpendingSourceId INTEGER NOT NULL,
                    CONSTRAINT FK_Expenses_ExpenseTags_ExpenseTagId FOREIGN KEY (ExpenseTagId) REFERENCES ExpenseTags (Id) ON DELETE RESTRICT,
                    CONSTRAINT FK_Expenses_SpendingSources_SpendingSourceId FOREIGN KEY (SpendingSourceId) REFERENCES SpendingSources (Id) ON DELETE RESTRICT
                );
                INSERT INTO Expenses_new (Id, Name, Amount, ExpenseCategory, ExpenseTagId, SpendingSourceId)
                SELECT Id, Name, Amount, ExpenseCategory, ExpenseTagId, SpendingSourceId FROM Expenses;
                DROP TABLE Expenses;
                ALTER TABLE Expenses_new RENAME TO Expenses;
                CREATE INDEX IX_Expenses_ExpenseTagId ON Expenses (ExpenseTagId);
                CREATE INDEX IX_Expenses_SpendingSourceId ON Expenses (SpendingSourceId);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE Expenses_old (
                    Id INTEGER NOT NULL CONSTRAINT PK_Expenses PRIMARY KEY AUTOINCREMENT,
                    Amount NUMERIC NOT NULL,
                    ExpenseCategory INTEGER NOT NULL,
                    ExpenseKind INTEGER NOT NULL,
                    ExpenseTagId INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    RecurringDate INTEGER NULL,
                    SpendingSourceId INTEGER NOT NULL,
                    CONSTRAINT FK_Expenses_ExpenseTags_ExpenseTagId FOREIGN KEY (ExpenseTagId) REFERENCES ExpenseTags (Id) ON DELETE RESTRICT,
                    CONSTRAINT FK_Expenses_SpendingSources_SpendingSourceId FOREIGN KEY (SpendingSourceId) REFERENCES SpendingSources (Id) ON DELETE RESTRICT
                );
                INSERT INTO Expenses_old (Id, Amount, ExpenseCategory, ExpenseKind, ExpenseTagId, IsActive, Name, RecurringDate, SpendingSourceId)
                SELECT
                    expense.Id,
                    expense.Amount,
                    expense.ExpenseCategory,
                    COALESCE((
                        SELECT CASE
                            WHEN recurring.Type = 1 THEN 1
                            ELSE 2
                        END
                        FROM RecurringTransactions recurring
                        WHERE recurring.SourceId = expense.SpendingSourceId
                          AND recurring.TagId = expense.ExpenseTagId
                          AND recurring.Name = expense.Name
                        ORDER BY recurring.Id DESC
                        LIMIT 1
                    ), 2),
                    expense.ExpenseTagId,
                    COALESCE((
                        SELECT recurring.IsEnabled
                        FROM RecurringTransactions recurring
                        WHERE recurring.SourceId = expense.SpendingSourceId
                          AND recurring.TagId = expense.ExpenseTagId
                          AND recurring.Name = expense.Name
                        ORDER BY recurring.Id DESC
                        LIMIT 1
                    ), 0),
                    expense.Name,
                    (
                        SELECT recurring.RecurringDate
                        FROM RecurringTransactions recurring
                        WHERE recurring.SourceId = expense.SpendingSourceId
                          AND recurring.TagId = expense.ExpenseTagId
                          AND recurring.Name = expense.Name
                        ORDER BY recurring.Id DESC
                        LIMIT 1
                    ),
                    expense.SpendingSourceId
                FROM Expenses expense;
                DROP TABLE Expenses;
                ALTER TABLE Expenses_old RENAME TO Expenses;
                CREATE INDEX IX_Expenses_ExpenseTagId ON Expenses (ExpenseTagId);
                CREATE INDEX IX_Expenses_SpendingSourceId ON Expenses (SpendingSourceId);
                """);

            migrationBuilder.DropTable(
                name: "RecurringTransactions");
        }
    }
}
