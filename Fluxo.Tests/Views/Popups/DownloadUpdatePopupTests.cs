using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class DownloadUpdatePopupTests
{
    [Fact]
    public void Popup_InheritsBasePopup_AndUsesLoaderPopupStructure()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "DownloadUpdatePopup.xaml"));

        Assert.Contains("<customControls:BasePopup", xaml);
        Assert.Contains("x:Class=\"Fluxo.Views.Popups.DownloadUpdatePopup\"", xaml);
        Assert.Contains("Background=\"{StaticResource Brush.Background.Elevated}\"", xaml);
        Assert.Contains("CornerRadius=\"22\"", xaml);
        Assert.Contains("<components:FluxoWave", xaml);
    }

    [Fact]
    public void Popup_ExposesThreeContentRows()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "DownloadUpdatePopup.xaml"));

        Assert.Contains("x:Name=\"VersionTextBlock\"", xaml);
        Assert.Contains("x:Name=\"ProgressTextBlock\"", xaml);
        Assert.Contains("Grid.Row=\"2\"", xaml);
        Assert.Contains("<components:FluxoWave", xaml);
    }

    [Fact]
    public void Popup_CloseButtonCancelsDownloadInsteadOfIgnoringClose()
    {
        var code = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "DownloadUpdatePopup.xaml.cs"));

        Assert.Contains("private readonly CancellationTokenSource _downloadCancellation = new();", code);
        Assert.Contains("_downloadCancellation.Cancel();", code);
        Assert.Contains("catch (OperationCanceledException) when (_isCancellationRequested)", code);
        Assert.Contains("public bool IsCanceled { get; private set; }", code);
    }
}
