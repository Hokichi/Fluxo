using CommunityToolkit.Mvvm.ComponentModel;
using CoreILogMemoryAction = Fluxo.Core.Interfaces.History.ILogMemoryAction;

namespace Fluxo.Services.History;

public sealed partial class LogMemoryEntry(CoreILogMemoryAction action) : ObservableObject
{
    internal CoreILogMemoryAction Action { get; } = action ?? throw new ArgumentNullException(nameof(action));

    public string Description => Action.Description;

    public string Title => Action.Title;

    public string Summary => Action.Summary;

    public string Details => Action.Details;

    [ObservableProperty]
    private bool _isReverted;
}
