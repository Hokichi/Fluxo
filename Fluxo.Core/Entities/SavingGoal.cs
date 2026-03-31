namespace Fluxo.Core.Entities
{
    public sealed class SavingGoal
    {
        public string Name { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime SavingEndDate { get; set; }
    }
}