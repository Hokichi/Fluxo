using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Controls
{
    public partial class DayOfWeekVM : ObservableObject
    {
        [ObservableProperty] private string _dayName;
        [ObservableProperty] private string _dayNumber;
        [ObservableProperty] private bool _isSelected;
    }
}