using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetAllocationPeriodStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentPeriodIndex",
                table: "BudgetAllocation",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRolloverPeriodStart",
                table: "BudgetAllocation",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "PeriodStart",
                table: "BudgetAllocation",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPeriodIndex",
                table: "BudgetAllocation");

            migrationBuilder.DropColumn(
                name: "LastRolloverPeriodStart",
                table: "BudgetAllocation");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "BudgetAllocation");
        }
    }
}
