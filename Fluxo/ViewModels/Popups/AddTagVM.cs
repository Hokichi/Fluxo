using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Fluxo.ViewModels.Popups;

public partial class AddTagVM : ObservableObject
{
    private const string DefaultColor = "#3FE0A1";
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;

    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _selectedColorHex = DefaultColor;

    public ObservableCollection<TagColorOptionVM> ColorOptions { get; } =
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

    public AddTagVM()
    {
        _initialState = CaptureState();
    }

    public bool HasChanges => _isChangeTrackingInitialized && !CaptureState().Equals(_initialState);
    public string PopupTitle => "Add New Tag";

    public void BeginChangeTracking()
    {
        _initialState = CaptureState();
        _isChangeTrackingInitialized = true;
        NotifyFormStateChanged();
    }

    partial void OnNameTextChanged(string value) => NotifyFormStateChanged();
    partial void OnSelectedColorHexChanged(string value) => NotifyFormStateChanged();

    public void AddCustomColorToFront(string hexCode)
    {
        var normalized = NormalizeHex(hexCode);
        var existingMatch = ColorOptions.FirstOrDefault(option =>
            string.Equals(option.HexCode, normalized, StringComparison.OrdinalIgnoreCase));
        if (existingMatch is not null)
            ColorOptions.Remove(existingMatch);

        ColorOptions.Insert(0, new TagColorOptionVM("Custom", normalized));
        ColorOptions.RemoveAt(ColorOptions.Count - 1);
        SelectedColorHex = normalized;
        NotifyFormStateChanged();
    }

    private FormState CaptureState()
    {
        var optionFingerprint = string.Join("|", ColorOptions.Select(option => option.HexCode));
        return new FormState(NameText ?? string.Empty, SelectedColorHex ?? DefaultColor, optionFingerprint);
    }

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(HasChanges));
    }

    private static string NormalizeHex(string value)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();
        return normalized.Length == 6 ? $"#{normalized}" : DefaultColor;
    }

    private readonly record struct FormState(string NameText, string SelectedColorHex, string OptionFingerprint);
}

public sealed record TagColorOptionVM(string Name, string HexCode);
