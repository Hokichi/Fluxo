using Fluxo.ViewModels.Popups;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class NotificationChecklistActionPopup : BasePopup
{
    public NotificationChecklistActionPopup(NotificationChecklistActionVM viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
