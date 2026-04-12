using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Popups;

public partial class AddTagVM : ObservableObject
{
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _selectedColorHex = "#3FE0A1";

    public IReadOnlyList<TagColorOptionVM> ColorOptions { get; } =
    [
        new("Mint", "#3FE0A1"),
        new("Ocean", "#4DA3FF"),
        new("Sun", "#FFB020"),
        new("Coral", "#FF7A59"),
        new("Rose", "#FF5C5C"),
        new("Lavender", "#A78BFA"),
        new("Sky", "#38BDF8"),
        new("Lime", "#84CC16"),
        new("Amber", "#F59E0B"),
        new("Peach", "#FB7185"),
        new("Slate", "#7C8796"),
        new("Teal", "#14B8A6")
    ];
}

public sealed record TagColorOptionVM(string Name, string HexCode);
