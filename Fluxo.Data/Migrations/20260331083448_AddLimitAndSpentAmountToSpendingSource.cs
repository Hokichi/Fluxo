using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLimitAndSpentAmountToSpendingSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Limit",
                table: "SpendingSources",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpentAmount",
                table: "SpendingSources",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Limit",
                table: "SpendingSources");

            migrationBuilder.DropColumn(
                name: "SpentAmount",
                table: "SpendingSources");
        }
    }
}
