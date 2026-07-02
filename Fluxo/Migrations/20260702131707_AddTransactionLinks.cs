using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "Transactions",
                newName: "SourceAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                newName: "IX_Transactions_SourceAccountId");

            migrationBuilder.AddColumn<int>(
                name: "GoalId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepaymentAccountId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_GoalId",
                table: "Transactions",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RepaymentAccountId",
                table: "Transactions",
                column: "RepaymentAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_RepaymentAccountId",
                table: "Transactions",
                column: "RepaymentAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_SourceAccountId",
                table: "Transactions",
                column: "SourceAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_SavingGoals_GoalId",
                table: "Transactions",
                column: "GoalId",
                principalTable: "SavingGoals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_RepaymentAccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_SourceAccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_SavingGoals_GoalId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_GoalId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_RepaymentAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "GoalId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RepaymentAccountId",
                table: "Transactions");

            migrationBuilder.RenameColumn(
                name: "SourceAccountId",
                table: "Transactions",
                newName: "AccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_SourceAccountId",
                table: "Transactions",
                newName: "IX_Transactions_AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
