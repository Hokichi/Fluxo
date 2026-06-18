using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseTagSpendingLimitAndPinnedSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ShowOnUI",
                table: "Accounts",
                newName: "PinnedOnUI");

            migrationBuilder.AddColumn<decimal>(
                name: "SpendingLimit",
                table: "ExpenseTags",
                type: "NUMERIC",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpendingLimit",
                table: "ExpenseTags");

            migrationBuilder.RenameColumn(
                name: "PinnedOnUI",
                table: "Accounts",
                newName: "ShowOnUI");
        }
    }
}
