namespace Fluxo.Core.Entities
{
    public sealed class ExpenseLog
    {
        public Expense Expense { get; set; }
        public SpendingSource SpendingSource { get; set; }
        public decimal Amount { get; set; }
        public DateTime DeductedOn { get; set; }
        public string Notes { get; set; }
    }
}