using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities
{
    public sealed class SpendingSource
    {
        public string Name { get; set; }
        public SpendingSourceType SpendingSourceType { get; set; }
        public decimal Limit { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal Balance { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal? InterestRate { get; set; }
    }
}