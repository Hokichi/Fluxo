using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.ViewModels.Messages;

namespace Fluxo.ViewModels.Shell;

public partial class MainViewModeToggleVM : ObservableRecipient
{
    [ObservableProperty]
    private MainContentViewMode _selectedMainContentViewMode = MainContentViewMode.Daily;

    public MainViewModeToggleVM(IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
    }

    public bool IsDailyViewSelected => SelectedMainContentViewMode == MainContentViewMode.Daily;

    public bool IsWeeklyViewSelected => SelectedMainContentViewMode == MainContentViewMode.Weekly;

    public bool IsMonthlyViewSelected => SelectedMainContentViewMode == MainContentViewMode.Monthly;

    public bool IsAllTimeViewSelected => SelectedMainContentViewMode == MainContentViewMode.AllTime;

    [RelayCommand]
    private void SetSelectedMainContentView(MainContentViewMode viewMode)
    {
        SelectedMainContentViewMode = viewMode;
        Messenger.Send(new ViewModeChangeMessage(viewMode));
    }

    partial void OnSelectedMainContentViewModeChanged(MainContentViewMode oldValue, MainContentViewMode newValue)
    {
        OnPropertyChanged(nameof(IsDailyViewSelected));
        OnPropertyChanged(nameof(IsWeeklyViewSelected));
        OnPropertyChanged(nameof(IsMonthlyViewSelected));
        OnPropertyChanged(nameof(IsAllTimeViewSelected));
    }
}
