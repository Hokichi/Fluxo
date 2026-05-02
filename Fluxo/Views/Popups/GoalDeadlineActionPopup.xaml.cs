using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class GoalDeadlineActionPopup : BasePopup
{
    public GoalDeadlineActionPopup(GoalDeadlineActionVM viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
