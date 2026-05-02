using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class AddSpendingSourceVMTests
{
    [Theory]
    [InlineData(SpendingSourceType.Checking, true)]
    [InlineData(SpendingSourceType.Cash, true)]
    [InlineData(SpendingSourceType.Saving, true)]
    [InlineData(SpendingSourceType.Credit, false)]
    [InlineData(SpendingSourceType.BNPL, false)]
    public void IsBalanceLike_MatchesExpectedTypes(SpendingSourceType sourceType, bool expected)
    {
        var sut = CreateSut();

        sut.SelectedSpendingSourceType = sourceType;

        Assert.Equal(expected, sut.IsBalanceLike);
    }

    private static AddSpendingSourceVM CreateSut()
    {
        return new AddSpendingSourceVM(
            mainViewModel: null!,
            appData: null!);
    }
}
