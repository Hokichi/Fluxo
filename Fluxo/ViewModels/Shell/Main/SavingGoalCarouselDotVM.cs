using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Shell.Main;

//TODO: Merge this into SavingGoalVM
public partial class SavingGoalCarouselDotVM : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;
}