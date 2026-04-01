using System;
using System.Collections.Generic;
using System.Text;

namespace Fluxo.Core.Entities
{
    public sealed class IncomeLog
    {
        public SpendingSource SpendingSource { get; set; }
        public decimal Amount { get; set; }
        public DateTime AddedOn { get; set; }
        public string Notes { get; set; }
    }
}