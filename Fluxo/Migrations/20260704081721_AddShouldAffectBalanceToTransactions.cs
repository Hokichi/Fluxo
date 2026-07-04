using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddShouldAffectBalanceToTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShouldAffectBalance",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Transactions SET ShouldAffectBalance = 1 WHERE IsIoU = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_ShouldAffectBalance_RequiresIoU",
                table: "Transactions",
                sql: "ShouldAffectBalance = 0 OR IsIoU = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_ShouldAffectBalance_RequiresIoU",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ShouldAffectBalance",
                table: "Transactions");
        }
    }
}
