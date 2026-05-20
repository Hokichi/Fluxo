using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    [Migration("20260520090000_MoveRecurringPeriodToRecurringTransactions")]
    public partial class MoveRecurringPeriodToRecurringTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurringPeriod",
                table: "SavingGoals");

            migrationBuilder.RenameColumn(
                name: "RecurringDate",
                table: "RecurringTransactions",
                newName: "RecurringTime");

            migrationBuilder.AddColumn<int>(
                name: "RecurringPeriod",
                table: "RecurringTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurringPeriod",
                table: "RecurringTransactions");

            migrationBuilder.RenameColumn(
                name: "RecurringTime",
                table: "RecurringTransactions",
                newName: "RecurringDate");

            migrationBuilder.AddColumn<int>(
                name: "RecurringPeriod",
                table: "SavingGoals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
