using Fluxo.Resources.CustomControls;

namespace Fluxo.Views.Popups;

public partial class FeaturePlaceholderPopup : BasePopup
{
    public FeaturePlaceholderPopup(string title, string message)
    {
        InitializeComponent();

        PlaceholderTitle = title;
        PlaceholderMessage = message;
        PopupTitle = title;
        DataContext = this;
    }

    public string PlaceholderTitle { get; }

    public string PlaceholderMessage { get; }
}
