using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Entities;

/// <summary>
/// Root base class for all entity ViewModels in Fluxo.
///
/// Inherits ObservableValidator which provides:
///   - INotifyPropertyChanged     (property binding)
///   - INotifyDataErrorInfo        (inline validation errors)
///   - ValidateAllProperties()     (trigger full validation pass)
///   - ValidateProperty(value, name) (per-field live validation)
///   - HasErrors / GetErrors()     (error state queries)
///
/// Convention:
///   - Id == 0  → entity is new (not yet persisted)
///   - Id  > 0  → entity already exists in the database
///   - IsNew is a convenience alias used in XAML converters / templates
///
/// Derived classes call ValidateAllProperties() in their constructor
/// (or after FromModel loading) so the UI shows an accurate initial
/// error state without waiting for the first keystroke.
/// </summary>
public abstract partial class BaseEntityVM : ObservableValidator
{
    [ObservableProperty]
    private int _id;

    /// <summary>True when this VM has never been saved to the database.</summary>
    public bool IsNew => Id == 0;

    /// <summary>
    /// Shorthand to check if the VM is in a valid, saveable state.
    /// Runs a full validation sweep first.
    /// </summary>
    public bool IsValid
    {
        get
        {
            ValidateAllProperties();
            return !HasErrors;
        }
    }

    /// <summary>
    /// Marks all properties as changed so the UI re-evaluates
    /// every binding — useful after FromModel loading.
    /// </summary>
    protected void RefreshAll() => OnPropertyChanged(string.Empty);
}
