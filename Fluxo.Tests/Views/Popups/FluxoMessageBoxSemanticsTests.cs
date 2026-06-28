using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class FluxoMessageBoxSemanticsTests
{
    [Theory]
    [InlineData("Fluxo", "Views", "Popups", "AddAccountPopup.xaml.cs")]
    [InlineData("Fluxo", "Views", "Popups", "AddSavingGoalPopup.xaml.cs")]
    [InlineData("Fluxo", "Views", "Popups", "AddTagPopup.xaml.cs")]
    public void DirtyClosePrompts_DescribeYesAsDiscardAndNoAsReturn(params string[] path)
    {
        var code = File.ReadAllText(Path.Combine(RepositoryPaths.Root, Path.Combine(path)));

        Assert.Contains("Close and discard changes?", code);
        Assert.DoesNotContain("\"Discard all changes?\"", code);
    }

    [Fact]
    public void TransactionDetailDirtyClosePrompt_DescribesYesAsDiscardAndNoAsReturn()
    {
        var code = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "Fluxo",
            "Views",
            "Popups",
            "TransactionDetailPopup.xaml.cs"));

        Assert.Contains("Close and discard unsaved changes?", code);
        Assert.DoesNotContain("keep editing", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MessageBoxPopup_MapsYesToPrimaryPerformButton_AndNoToCancelButton()
    {
        var code = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "Fluxo.Resources",
            "Components",
            "MessageBoxPopup.xaml.cs"));

        Assert.Contains("ConfigureButton(PrimaryButton, \"Yes\", MessageBoxResult.Yes, true);", code);
        Assert.Contains("ConfigureButton(SecondaryButton, \"No\", MessageBoxResult.No, isCancel: true);", code);
        Assert.Contains("MessageBoxButton.YesNo => MessageBoxResult.No", code);
    }
}
