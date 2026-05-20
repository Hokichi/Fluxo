using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddSpendingAndSavingSchemaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaximumSpending",
                table: "SpendingSources",
                type: "NUMERIC",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumPayment",
                table: "SpendingSources",
                type: "NUMERIC",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "SavingEndDate",
                table: "SavingGoals",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "RecurringPeriod",
                table: "SavingGoals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: ["Name", "Value"],
                values: ["AllocationPeriod", "Monthly"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumSpending",
                table: "SpendingSources");

            migrationBuilder.DropColumn(
                name: "MinimumPayment",
                table: "SpendingSources");

            migrationBuilder.DropColumn(
                name: "RecurringPeriod",
                table: "SavingGoals");

            migrationBuilder.DeleteData(
                table: "UserSettings",
                keyColumn: "Name",
                keyValue: "AllocationPeriod");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SavingEndDate",
                table: "SavingGoals",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
