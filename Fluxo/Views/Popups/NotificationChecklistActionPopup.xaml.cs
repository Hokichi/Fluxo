using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class NotificationChecklistActionPopup : BasePopup
{
    public NotificationChecklistActionPopup(NotificationChecklistActionVM viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
