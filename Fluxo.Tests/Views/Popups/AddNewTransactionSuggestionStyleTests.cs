using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AddNewTransactionSuggestionStyleTests
{
    [Fact]
    public void TransactionNameSuggestions_UseHoverBackgroundResource()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Contains("x:Key=\"TransactionNameSuggestionListBoxItemStyle\"", xaml);
        Assert.Contains("TargetName=\"ItemBackground\" Property=\"Background\" Value=\"{DynamicResource Brush.Background.Hover}\"", xaml);
        Assert.Equal(2, xaml.Split("ItemContainerStyle=\"{StaticResource TransactionNameSuggestionListBoxItemStyle}\"").Length - 1);
    }
}
