using Fluxo.Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using Fluxo.Core.Entities;

namespace Fluxo.Core.Filters
{
    public class ExpenseFilter
    {
        public string Name { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public ExpenseCategory? Category { get; set; }
        public ExpenseKind? Kind { get; set; }
        public ExpenseTag? Tag { get; set; }
        public int? TagId { get; set; }
        public bool ShouldFilterDeletion { get; set; }
    }
}