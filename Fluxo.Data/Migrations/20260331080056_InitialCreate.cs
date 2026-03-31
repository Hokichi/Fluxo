using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    HexCode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    SavingEndDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingGoals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpendingSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SpendingSourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Balance = table.Column<decimal>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InterestRate = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendingSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SpendingSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpenseTagId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExpenseKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpenseCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    RecurringDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseTags_ExpenseTagId",
                        column: x => x.ExpenseTagId,
                        principalTable: "ExpenseTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_SpendingSources_SpendingSourceId",
                        column: x => x.SpendingSourceId,
                        principalTable: "SpendingSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExpenseId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpendingSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    DeductedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseLogs_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseLogs_SpendingSources_SpendingSourceId",
                        column: x => x.SpendingSourceId,
                        principalTable: "SpendingSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_ExpenseId",
                table: "ExpenseLogs",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_SpendingSourceId",
                table: "ExpenseLogs",
                column: "SpendingSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseTagId",
                table: "Expenses",
                column: "ExpenseTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_SpendingSourceId",
                table: "Expenses",
                column: "SpendingSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseLogs");

            migrationBuilder.DropTable(
                name: "SavingGoals");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "ExpenseTags");

            migrationBuilder.DropTable(
                name: "SpendingSources");
        }
    }
}
