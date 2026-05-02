using System;
using System.Collections.Generic;
using System.Text;
using Fluxo.Core.Entities;

namespace Fluxo.Core.Filters
{
    public class IncomeLogFilter
    {
        public SpendingSource? SpendingSource { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}