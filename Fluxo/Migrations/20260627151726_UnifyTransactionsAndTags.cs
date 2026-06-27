using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class UnifyTransactionsAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_ExpenseTags_TagId",
                table: "RecurringTransactions");

            migrationBuilder.RenameTable(name: "ExpenseTags", newName: "Tags");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    ExpenseCategory = table.Column<int>(type: "INTEGER", nullable: true),
                    TagId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsForDeletion = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsIoU = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsExcludedFromBudget = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transactions_Amount", "Amount >= 0");
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Transactions_ParentTransactionId",
                        column: x => x.ParentTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO Transactions
                    (Id, Type, AccountId, Name, Amount, OccurredOn, Notes, ExpenseCategory, TagId,
                     ParentTransactionId, IsPinned, IsForDeletion, IsIoU, IsExcludedFromBudget)
                SELECT l.Id, 0, l.AccountId, e.Name, ABS(l.Amount), l.DeductedOn, COALESCE(l.Notes, ''),
                       e.ExpenseCategory, e.ExpenseTagId, l.ParentLogId,
                       COALESCE(l.IsPinned, 0), COALESCE(l.IsForDeletion, 0),
                       CASE WHEN COALESCE(l.IsLend, 0) = 1 OR COALESCE(e.IsLend, 0) = 1 THEN 1 ELSE 0 END, 0
                FROM ExpenseLogs l JOIN Expenses e ON e.Id = l.ExpenseId;

                INSERT INTO Transactions
                    (Id, Type, AccountId, Name, Amount, OccurredOn, Notes, ExpenseCategory, TagId,
                     ParentTransactionId, IsPinned, IsForDeletion, IsIoU, IsExcludedFromBudget)
                SELECT COALESCE((SELECT MAX(Id) FROM ExpenseLogs), 0) + ROW_NUMBER() OVER (ORDER BY i.Id),
                       1, i.AccountId, COALESCE(i.Name, 'Income'), ABS(i.Amount), i.AddedOn, COALESCE(i.Notes, ''),
                       NULL, NULL, NULL, COALESCE(i.IsPinned, 0), COALESCE(i.IsForDeletion, 0), COALESCE(i.IsDebt, 0), 0
                FROM IncomeLogs i;

                CREATE TEMP TABLE __UnifiedTransactionGuard (Valid INTEGER NOT NULL CHECK (Valid = 1));
                INSERT INTO __UnifiedTransactionGuard
                SELECT CASE WHEN
                    (SELECT COUNT(*) FROM Transactions) =
                    (SELECT COUNT(*) FROM ExpenseLogs) + (SELECT COUNT(*) FROM IncomeLogs)
                    AND NOT EXISTS (SELECT 1 FROM Transactions WHERE Name IS NULL OR Notes IS NULL OR Amount < 0)
                    AND NOT EXISTS (
                        SELECT 1 FROM Transactions child
                        WHERE child.ParentTransactionId IS NOT NULL
                          AND NOT EXISTS (SELECT 1 FROM Transactions parent WHERE parent.Id = child.ParentTransactionId))
                    THEN 1 ELSE 0 END;
                DROP TABLE __UnifiedTransactionGuard;
                """);

            migrationBuilder.DropTable(name: "ExpenseLogs");
            migrationBuilder.DropTable(name: "IncomeLogs");
            migrationBuilder.DropTable(name: "Expenses");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_IsDefault",
                table: "Accounts",
                column: "IsDefault",
                unique: true,
                filter: "IsDefault = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ParentTransactionId",
                table: "Transactions",
                column: "ParentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TagId",
                table: "Transactions",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_Tags_TagId",
                table: "RecurringTransactions",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT OR REPLACE INTO sqlite_sequence(name, seq)
                VALUES ('Transactions', COALESCE((SELECT MAX(Id) FROM Transactions), 0));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Unified transactions cannot be downgraded without data loss.");
#if false
            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_Tags_TagId",
                table: "RecurringTransactions");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_IsDefault",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Accounts");

            migrationBuilder.CreateTable(
                name: "ExpenseTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HexCode = table.Column<string>(type: "TEXT", nullable: false),
                    IsSystemTag = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SpendingLimit = table.Column<decimal>(type: "NUMERIC", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    IsDebt = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsForDeletion = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeLogs_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpenseTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    ExpenseCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLend = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseTags_ExpenseTagId",
                        column: x => x.ExpenseTagId,
                        principalTable: "ExpenseTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpenseId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentLogId = table.Column<int>(type: "INTEGER", nullable: true),
                    Amount = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    DeductedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsForDeletion = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLend = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseLogs_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseLogs_ExpenseLogs_ParentLogId",
                        column: x => x.ParentLogId,
                        principalTable: "ExpenseLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseLogs_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_AccountId",
                table: "ExpenseLogs",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_ExpenseId",
                table: "ExpenseLogs",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_ParentLogId",
                table: "ExpenseLogs",
                column: "ParentLogId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_AccountId",
                table: "Expenses",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseTagId",
                table: "Expenses",
                column: "ExpenseTagId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeLogs_AccountId",
                table: "IncomeLogs",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_ExpenseTags_TagId",
                table: "RecurringTransactions",
                column: "TagId",
                principalTable: "ExpenseTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
#endif
        }
    }
}
