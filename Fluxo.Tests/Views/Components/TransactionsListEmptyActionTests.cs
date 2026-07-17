using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Components;

public sealed class TransactionsListEmptyActionTests
{
    [Fact]
    public void TransactionsList_ReplacesEmptyTextWithDashedActionButton()
    {
        var xaml = File.ReadAllText(ResolveTransactionsListXamlPath());

        Assert.DoesNotContain("No entry found", xaml);
        Assert.Contains("Content=\"{Binding EmptyActionText", xaml);
        Assert.Contains("Click=\"OnEmptyActionButtonClick\"", xaml);
        Assert.Contains("Style=\"{StaticResource AccountAddButtonStyle}\"", xaml);
        Assert.Contains("<MultiDataTrigger.Conditions>", xaml);
        Assert.Contains("Binding IsListEmpty", xaml);
        Assert.Contains("Binding IsEmptyActionVisible", xaml);
        Assert.Contains("Panel.ZIndex=\"1\"", xaml);
    }

    [Fact]
    public void TransactionsListCodeBehind_ExposesEmptyActionContract()
    {
        var source = File.ReadAllText(ResolveTransactionsListCodeBehindPath());

        Assert.Contains("EmptyActionTextProperty", source);
        Assert.Contains("EmptyActionParameterProperty", source);
        Assert.Contains("IsEmptyActionVisibleProperty", source);
        Assert.Contains("public bool IsEmptyActionVisible", source);
        Assert.Contains("public event RoutedEventHandler? EmptyActionClick;", source);
        Assert.Contains("private void OnEmptyActionButtonClick(object sender, RoutedEventArgs e)", source);
    }

    [Fact]
    public void TransactionsList_UsesRecentActivityAndAmountHeaders()
    {
        var xaml = File.ReadAllText(ResolveTransactionsListXamlPath());
        var source = File.ReadAllText(ResolveTransactionsListCodeBehindPath());

        Assert.Contains("Text=\"RECENT ACTIVITIES\"", xaml);
        Assert.Contains("Foreground=\"{Binding DotColor", xaml);
        Assert.Contains("Text=\"ITEM\"", xaml);
        Assert.Contains("Text=\"AMOUNT\"", xaml);
        Assert.DoesNotContain("alloc", xaml);
        Assert.DoesNotContain("ProgressToArcGeometryConverter", xaml);
        Assert.DoesNotContain("SpentAmountProperty", source);
        Assert.DoesNotContain("RemainingAmountProperty", source);
    }

    [Fact]
    public void TransactionsList_ItemsUseTransparentBackgroundAndTagColorStrip()
    {
        var xaml = File.ReadAllText(ResolveTransactionsListXamlPath());

        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("Background=\"{Binding TagHexCode", xaml);
        Assert.DoesNotContain("Background=\"{Binding DotColor", xaml);
        Assert.Contains("Background=\"{StaticResource Brush.Background.Hover}\"", xaml);
    }

    private static string ResolveTransactionsListXamlPath() =>
        ResolveRepositoryFile("Fluxo.Resources", "Components", "TransactionsList.xaml");

    private static string ResolveTransactionsListCodeBehindPath() =>
        ResolveRepositoryFile("Fluxo.Resources", "Components", "TransactionsList.xaml.cs");

    private static string ResolveRepositoryFile(params string[] relativeSegments)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Fluxo.slnx")))
                return Path.Combine(currentDirectory.FullName, Path.Combine(relativeSegments));

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing 'Fluxo.slnx'.");
    }
}
