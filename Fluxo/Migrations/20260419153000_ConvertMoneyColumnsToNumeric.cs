using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class ConvertMoneyColumnsToNumeric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "SpentAmount",
                table: "SpendingSources",
                type: "NUMERIC",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "InterestRate",
                table: "SpendingSources",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "SpendingSources",
                type: "NUMERIC",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "AccountLimit",
                table: "SpendingSources",
                type: "NUMERIC",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetAmount",
                table: "SavingGoals",
                type: "NUMERIC",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentAmount",
                table: "SavingGoals",
                type: "NUMERIC",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "IncomeLogs",
                type: "NUMERIC",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Expenses",
                type: "NUMERIC",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "ExpenseLogs",
                type: "NUMERIC",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "SpentAmount",
                table: "SpendingSources",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMERIC");

            migrationBuilder.AlterColumn<decimal>(
                name: "InterestRate",
                table: "SpendingSources",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "REAL",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "SpendingSources",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMERIC");

            migrationBuilder.AlterColumn<decimal>(
                name: "AccountLimit",
                table: "SpendingSources",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMERIC");

            migrationBuilder.AlterColumn<decimal>(
                name: "TargetAmount",
                table: "SavingGoals",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMERIC");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentAmount",
                table: "SavingGoals",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMERIC");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "IncomeLogs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMERIC");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Expenses",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMERIC");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "ExpenseLogs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "NUMERIC");
        }
    }
}
