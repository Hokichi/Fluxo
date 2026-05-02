namespace Fluxo.Core.Entities;

public sealed class Notification
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public bool IsCleared { get; set; }
    public bool IsForDeletion { get; set; }
}
