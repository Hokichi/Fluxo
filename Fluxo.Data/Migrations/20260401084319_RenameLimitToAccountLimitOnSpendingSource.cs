using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameLimitToAccountLimitOnSpendingSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Limit",
                table: "SpendingSources",
                newName: "AccountLimit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccountLimit",
                table: "SpendingSources",
                newName: "Limit");
        }
    }
}
