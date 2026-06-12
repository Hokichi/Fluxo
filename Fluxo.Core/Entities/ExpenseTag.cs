namespace Fluxo.Core.Entities;

public sealed class ExpenseTag
{
    public int Id { get; set; }
    public bool IsSystemTag { get; set; } = false;
    public string Name { get; set; }
    public string HexCode { get; set; }

    // TODO: Add a per-tag spending limit
}