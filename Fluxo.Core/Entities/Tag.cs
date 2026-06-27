namespace Fluxo.Core.Entities;

public sealed class Tag
{
    public int Id { get; set; }
    public bool IsSystemTag { get; set; } = false;
    public string Name { get; set; }
    public string HexCode { get; set; }
    public decimal? SpendingLimit { get; set; }
}
