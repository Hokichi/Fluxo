namespace Fluxo.Core.DTO;

public class TagDto
{
    public int Id { get; set; }
    public bool IsSystemTag { get; set; } = false;
    public string Name { get; set; } = string.Empty;
    public string HexCode { get; set; } = string.Empty;
    public decimal? SpendingLimit { get; set; }
}
