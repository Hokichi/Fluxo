using Fluxo.ViewModels.Popups;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class GoalDeadlineActionPopup : BasePopup
{
    public GoalDeadlineActionPopup(GoalDeadlineActionVM viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
