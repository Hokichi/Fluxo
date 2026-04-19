using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Messages;

namespace Fluxo.ViewModels.Shell.Main;

public partial class MainViewModeToggleVM : ObservableRecipient, IRecipient<SpinnerPeriodStateChangedMessage>
{
    [ObservableProperty]
    private MainContentViewMode _selectedMainContentViewMode = MainContentViewMode.Daily;

    [ObservableProperty]
    private string _moveToCurrentLabel = "Move to today";

    [ObservableProperty]
    private bool _isAtCurrentPeriod = true;

    [ObservableProperty]
    private bool _isSpinnerVisible = true;

    public MainViewModeToggleVM(IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        IsActive = true;
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

    [RelayCommand]
    private void MoveToCurrentPeriod()
    {
        Messenger.Send(new MoveToCurrentPeriodRequestedMessage());
    }

    public void Receive(SpinnerPeriodStateChangedMessage message)
    {
        IsAtCurrentPeriod = message.Value.IsAtCurrentPeriod;
        IsSpinnerVisible = message.Value.IsSpinnerVisible;
        MoveToCurrentLabel = message.Value.MoveToCurrentLabel;
    }

    partial void OnSelectedMainContentViewModeChanged(MainContentViewMode oldValue, MainContentViewMode newValue)
    {
        OnPropertyChanged(nameof(IsDailyViewSelected));
        OnPropertyChanged(nameof(IsWeeklyViewSelected));
        OnPropertyChanged(nameof(IsMonthlyViewSelected));
        OnPropertyChanged(nameof(IsAllTimeViewSelected));
    }
}
