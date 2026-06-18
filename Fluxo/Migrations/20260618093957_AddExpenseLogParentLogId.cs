using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseLogParentLogId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentLogId",
                table: "ExpenseLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLogs_ParentLogId",
                table: "ExpenseLogs",
                column: "ParentLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseLogs_ExpenseLogs_ParentLogId",
                table: "ExpenseLogs",
                column: "ParentLogId",
                principalTable: "ExpenseLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseLogs_ExpenseLogs_ParentLogId",
                table: "ExpenseLogs");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseLogs_ParentLogId",
                table: "ExpenseLogs");

            migrationBuilder.DropColumn(
                name: "ParentLogId",
                table: "ExpenseLogs");
        }
    }
}
