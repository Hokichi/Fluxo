using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fluxo.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Name);
                });

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: ["Name", "Value"],
                values: new object[,]
                {
                    { "DeadlineReminderDays", "7" },
                    { "BudgetUsageWarningPercentage", "0.90" },
                    { "CreditUsageWarningPercentage", "0.30" },
                    { "LowAccountBalancePercentage", "0.20" },
                    { "NeedsThreshold", "50" },
                    { "WantsThreshold", "30" },
                    { "InvestThreshold", "20" },
                    { "Salary", "0" },
                    { "IsFixedExpensesDeductionNotifEnabled", "false" },
                    { "IsLowCreditNotifEnabled", "false" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "UserSettings",
                keyColumn: "Name",
                keyValues:
                [
                    "DeadlineReminderDays",
                    "BudgetUsageWarningPercentage",
                    "CreditUsageWarningPercentage",
                    "LowAccountBalancePercentage",
                    "NeedsThreshold",
                    "WantsThreshold",
                    "InvestThreshold",
                    "Salary",
                    "IsFixedExpensesDeductionNotifEnabled",
                    "IsLowCreditNotifEnabled"
                ]);

            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
