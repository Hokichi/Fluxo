using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndDeductSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeductSource",
                table: "SpendingSources",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Header = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsCleared = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsForDeletion = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeductSource",
                table: "SpendingSources");
        }
    }
}
