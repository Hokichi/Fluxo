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

    [Fact]
    public async Task SaveAsync_WhenCreditMaximumSpendingExceedsAvailableCredit_ReturnsWarningAndDoesNotSave()
    {
        var saveWasCalled = false;
        var sut = CreateSut(_ =>
        {
            saveWasCalled = true;
            return Task.FromResult(AddSpendingSourceVM.AddSpendingSourceResult.Success());
        });

        ConfigureValidCreditFields(sut);
        sut.AccountLimitText = 1000m;
        sut.SpentAmountText = 900m;
        sut.MaximumSpendingText = 101m;

        var result = await sut.SaveAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("Maximum spending cannot exceed the available credit.", result.ErrorMessage);
        Assert.Equal(AddSpendingSourceVM.AddSpendingSourceFailurePresentation.ToastWarning, result.FailurePresentation);
        Assert.False(saveWasCalled);
    }

    [Fact]
    public async Task SaveAsync_WhenSavingApyExceeds100_ReturnsWarningAndDoesNotSave()
    {
        var saveWasCalled = false;
        var sut = CreateSut(_ =>
        {
            saveWasCalled = true;
            return Task.FromResult(AddSpendingSourceVM.AddSpendingSourceResult.Success());
        });

        sut.NameText = "Emergency Savings";
        sut.SelectedSpendingSourceType = SpendingSourceType.Saving;
        sut.PrimaryAmountText = 500m;
        sut.ApyText = 101m;

        var result = await sut.SaveAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("APY cannot exceed 100%.", result.ErrorMessage);
        Assert.Equal(AddSpendingSourceVM.AddSpendingSourceFailurePresentation.ToastWarning, result.FailurePresentation);
        Assert.False(saveWasCalled);
    }

    [Fact]
    public async Task SaveAsync_WhenCreditMinimumPaymentExceeds100_ReturnsWarningAndDoesNotSave()
    {
        var saveWasCalled = false;
        var sut = CreateSut(_ =>
        {
            saveWasCalled = true;
            return Task.FromResult(AddSpendingSourceVM.AddSpendingSourceResult.Success());
        });

        ConfigureValidCreditFields(sut);
        sut.MinimumPaymentText = 101m;

        var result = await sut.SaveAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("Minimum payment cannot exceed 100%.", result.ErrorMessage);
        Assert.Equal(AddSpendingSourceVM.AddSpendingSourceFailurePresentation.ToastWarning, result.FailurePresentation);
        Assert.False(saveWasCalled);
    }

    [Fact]
    public void SelectedSpendingSourceType_WhenBnpl_ClearsMaximumSpendingAndMinimumPayment()
    {
        var sut = CreateSut();
        sut.SelectedSpendingSourceType = SpendingSourceType.Credit;
        sut.MaximumSpendingText = 250m;
        sut.MinimumPaymentText = 10m;

        sut.SelectedSpendingSourceType = SpendingSourceType.BNPL;

        Assert.Equal(0m, sut.MaximumSpendingText);
        Assert.Equal(0m, sut.MinimumPaymentText);
    }

    private static AddSpendingSourceVM CreateSut(
        Func<AddSpendingSourceVM.AddSpendingSourceInput, Task<AddSpendingSourceVM.AddSpendingSourceResult>>? saveDraftAsync = null)
    {
        return new AddSpendingSourceVM(
            mainViewModel: null!,
            appData: null!,
            saveDraftAsync: saveDraftAsync);
    }

    private static void ConfigureValidCreditFields(AddSpendingSourceVM sut)
    {
        sut.NameText = "Credit Card";
        sut.SelectedSpendingSourceType = SpendingSourceType.Credit;
        sut.AccountLimitText = 1000m;
        sut.SpentAmountText = 100m;
        sut.MaximumSpendingText = 100m;
        sut.MinimumPaymentText = 10m;
        sut.MonthlyDueDateText = "15";
        sut.DeductSources.Add(new AddSpendingSourceVM.DeductSourceOption(1, "Checking", SpendingSourceType.Checking));
        sut.SelectedDeductSource = 1;
    }
}
