using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Fluxo.Core.Entities;

[Table("BudgetConfigs")]
public class BudgetConfig
{
    public int Id { get; set; }

    /// <summary>1-based month number (1 = January).</summary>
    public int Month { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "REAL")]
    public decimal NeedsPercentage { get; set; } = 50;

    [Column(TypeName = "REAL")]
    public decimal WantsPercentage { get; set; } = 30;

    [Column(TypeName = "REAL")]
    public decimal SavingsPercentage { get; set; } = 20;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}