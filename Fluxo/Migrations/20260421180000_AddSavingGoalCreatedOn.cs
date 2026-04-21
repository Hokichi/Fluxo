using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    [DbContext(typeof(FluxoDbContext))]
    [Migration("20260421180000_AddSavingGoalCreatedOn")]
    public partial class AddSavingGoalCreatedOn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "SavingGoals",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.Sql("UPDATE SavingGoals SET CreatedOn = CURRENT_TIMESTAMP");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "SavingGoals");
        }
    }
}
