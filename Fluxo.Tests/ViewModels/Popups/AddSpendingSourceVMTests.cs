using System.Globalization;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Helpers;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class AddSpendingSourceVMTests
{
    [Fact]
    public void Constructor_DefaultsMonthlyDueDateToCurrentDay()
    {
        var sut = CreateSut();
        var expected = MonthlyDueDateHelper.Normalize(DateTime.Today.Day)?
            .ToString(CultureInfo.InvariantCulture);

        Assert.Equal(expected, sut.MonthlyDueDateText);
    }

    [Fact]
    public void PopupTitle_WhenEditingSpendingSource_UsesSpendingSourceCopy()
    {
        var sut = new AddSpendingSourceVM(
            mainViewModel: null!,
            appData: null!)
        {
            EditingId = 7
        };

        Assert.Equal("Edit Spending Source", sut.PopupTitle);
    }

    [Fact]
    public void InitializeFromSpendingSource_LoadsEditableFields()
    {
        var sut = CreateSut();
        var source = new Fluxo.Core.Entities.SpendingSource
        {
            Id = 7,
            Name = "Visa",
            SpendingSourceType = SpendingSourceType.Credit,
            Balance = 0m,
            SpentAmount = 120m,
            AccountLimit = 1_000m,
            MaximumSpending = 800m,
            MinimumPayment = 5m,
            MonthlyDueDate = 15,
            DeductSource = 2,
            ShowOnUI = false,
            IsEnabled = false
        };

        sut.InitializeFromSpendingSource(source);

        Assert.Equal(7, sut.EditingId);
        Assert.Equal("Visa", sut.NameText);
        Assert.Equal(SpendingSourceType.Credit, sut.SelectedSpendingSourceType);
        Assert.Equal(120m, sut.SpentAmountText);
        Assert.Equal(1_000m, sut.AccountLimitText);
        Assert.Equal(800m, sut.MaximumSpendingText);
        Assert.Equal(5m, sut.MinimumPaymentText);
        Assert.Equal("15", sut.MonthlyDueDateText);
        Assert.Equal(2, sut.SelectedDeductSource);
        Assert.False(sut.ShowOnUI);
        Assert.False(sut.IsEnabled);
    }

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
    public async Task SaveAsync_WhenCreditMaximumSpendingExceedsAccountLimit_ReturnsWarningAndDoesNotSave()
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
        sut.MaximumSpendingText = 1001m;
        sut.MarkMaximumSpendingModified();

        var result = await sut.SaveAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("Maximum spending cannot exceed the account limit.", result.ErrorMessage);
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateNameField_WhenNameIsEmptyOrWhitespace_ShowsRequiredHint(string name)
    {
        var sut = CreateSut();
        sut.NameText = name;

        sut.ValidateNameField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Required", sut.NameValidationHint);
    }

    [Fact]
    public void ValidateNameField_WhenNameContainsControlCharacter_ShowsInvalidNameHint()
    {
        var sut = CreateSut();
        sut.NameText = "Bad\u0001Name";

        sut.ValidateNameField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Invalid Name", sut.NameValidationHint);
    }

    [Fact]
    public void ValidateNameField_WhenNameExceedsMaximumLength_ShowsTooLongHint()
    {
        var sut = CreateSut();
        sut.NameText = new string('A', 257);

        sut.ValidateNameField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Too Long", sut.NameValidationHint);
    }

    [Fact]
    public void ValidateMaximumSpendingField_WhenBalanceLikeMaximumExceedsBalance_ShowsLimitHint()
    {
        var sut = CreateSut();
        sut.NameText = "Checking";
        sut.SelectedSpendingSourceType = SpendingSourceType.Checking;
        sut.PrimaryAmountText = 100m;
        sut.MaximumSpendingText = 101m;

        sut.ValidateMaximumSpendingField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Exceeds Limit", sut.MaximumSpendingValidationHint);
    }

    [Fact]
    public void ValidateMaximumSpendingField_WhenCreditMaximumExceedsAccountLimit_ShowsLimitHint()
    {
        var sut = CreateSut();
        ConfigureValidCreditFields(sut);
        sut.AccountLimitText = 100m;
        sut.SpentAmountText = 0m;
        sut.MaximumSpendingText = 101m;

        sut.ValidateMaximumSpendingField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Exceeds Limit", sut.MaximumSpendingValidationHint);
    }

    [Fact]
    public void ValidateSpentAmountField_WhenCreditSpentAmountExceedsAccountLimit_ShowsLimitHint()
    {
        var sut = CreateSut();
        ConfigureValidCreditFields(sut);
        sut.AccountLimitText = 100m;
        sut.SpentAmountText = 101m;

        sut.ValidateSpentAmountField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Exceeds Limit", sut.SpentAmountValidationHint);
    }

    [Fact]
    public void ValidateMinimumPaymentField_WhenCreditMinimumPaymentIsZero_ShowsRequiredHint()
    {
        var sut = CreateSut();
        ConfigureValidCreditFields(sut);
        sut.MinimumPaymentText = 0m;

        sut.ValidateMinimumPaymentField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Required", sut.MinimumPaymentValidationHint);
    }

    [Fact]
    public void ValidateApyField_WhenSavingApyIsZero_ShowsRequiredHint()
    {
        var sut = CreateSut();
        sut.NameText = "Savings";
        sut.SelectedSpendingSourceType = SpendingSourceType.Saving;
        sut.PrimaryAmountText = 100m;
        sut.ApyText = 0m;

        sut.ValidateApyField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Required", sut.ApyValidationHint);
    }

    [Fact]
    public void SelectedSpendingSourceTypeChanged_ClearsNameValidation()
    {
        var sut = CreateSut();
        sut.NameText = " ";
        sut.ValidateNameField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Required", sut.NameValidationHint);

        sut.SelectedSpendingSourceType = SpendingSourceType.Credit;

        Assert.Empty(sut.GetErrors(nameof(AddSpendingSourceVM.NameText)));
        Assert.Equal(string.Empty, sut.NameValidationHint);
    }

    [Fact]
    public void SelectedSpendingSourceTypeChanged_ClearsMaximumSpendingValidation()
    {
        var sut = CreateSut();
        ConfigureValidCreditFields(sut);
        sut.AccountLimitText = 100m;
        sut.MaximumSpendingText = 101m;
        sut.ValidateMaximumSpendingField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Exceeds Limit", sut.MaximumSpendingValidationHint);

        sut.SelectedSpendingSourceType = SpendingSourceType.BNPL;

        Assert.Empty(sut.GetErrors(nameof(AddSpendingSourceVM.MaximumSpendingText)));
        Assert.Equal(string.Empty, sut.MaximumSpendingValidationHint);
    }

    [Fact]
    public void SelectedSpendingSourceTypeChanged_ClearsInvalidNameValidationButKeepsSaveDisabled()
    {
        var sut = CreateSut();
        sut.NameText = new string('A', 257);
        sut.ValidateNameField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Too Long", sut.NameValidationHint);

        sut.SelectedSpendingSourceType = SpendingSourceType.Cash;

        Assert.Empty(sut.GetErrors(nameof(AddSpendingSourceVM.NameText)));
        Assert.Equal(string.Empty, sut.NameValidationHint);
        Assert.False(sut.CanSave);
    }

    [Fact]
    public async Task ValidateNameField_WhenSourceNameAlreadyExists_ShowsExistingNameHint()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetSpendingSourcesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpendingSource>>(
            [
                new SpendingSource
                {
                    Id = 1,
                    Name = "Checking",
                    SpendingSourceType = SpendingSourceType.Checking,
                    IsEnabled = true
                }
            ]));
        var sut = new AddSpendingSourceVM(
            mainViewModel: null!,
            appData: appData);
        await sut.LoadDeductSourcesAsync();
        sut.NameText = "checking";

        sut.ValidateNameField();

        Assert.True(sut.HasErrors);
        Assert.Equal("Existing Name", sut.NameValidationHint);
    }

    [Fact]
    public void HasChanges_IgnoresDueDateDeductSourceMaximumSpendingAndSourceType()
    {
        var sut = CreateSut();
        sut.DeductSources.Add(new AddSpendingSourceVM.DeductSourceOption(1, "Checking", SpendingSourceType.Checking));
        sut.BeginChangeTracking();

        sut.MonthlyDueDateText = "12";
        sut.SelectedDeductSource = 1;
        sut.MaximumSpendingText = 500m;
        sut.SelectedSpendingSourceType = SpendingSourceType.Credit;

        Assert.False(sut.HasChanges);
    }

    [Fact]
    public void MaximumSpendingPlaceholderText_UsesBalanceForBalanceLikeSources()
    {
        var sut = CreateSut();
        sut.SelectedSpendingSourceType = SpendingSourceType.Checking;
        sut.PrimaryAmountText = 1250.50m;

        Assert.Equal("1250.50", sut.MaximumSpendingPlaceholderText);
    }

    [Fact]
    public void MaximumSpendingPlaceholderText_UsesAccountLimitForCreditSources()
    {
        var sut = CreateSut();
        sut.SelectedSpendingSourceType = SpendingSourceType.Credit;
        sut.AccountLimitText = 2500m;

        Assert.Equal("2500", sut.MaximumSpendingPlaceholderText);
    }

    [Fact]
    public async Task SaveAsync_WhenMaximumSpendingIsUnmodified_PersistsPlaceholderValue()
    {
        AddSpendingSourceVM.AddSpendingSourceInput? savedInput = null;
        var sut = CreateSut(input =>
        {
            savedInput = input;
            return Task.FromResult(AddSpendingSourceVM.AddSpendingSourceResult.Success());
        });
        sut.NameText = "Checking";
        sut.SelectedSpendingSourceType = SpendingSourceType.Checking;
        sut.PrimaryAmountText = 750m;

        var result = await sut.SaveAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(750m, savedInput?.MaximumSpending);
    }

    [Fact]
    public async Task SaveAsync_WhenMaximumSpendingIsModified_PersistsEditedValue()
    {
        AddSpendingSourceVM.AddSpendingSourceInput? savedInput = null;
        var sut = CreateSut(input =>
        {
            savedInput = input;
            return Task.FromResult(AddSpendingSourceVM.AddSpendingSourceResult.Success());
        });
        sut.NameText = "Checking";
        sut.SelectedSpendingSourceType = SpendingSourceType.Checking;
        sut.PrimaryAmountText = 750m;
        sut.MaximumSpendingText = 100m;
        sut.MarkMaximumSpendingModified();

        var result = await sut.SaveAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, savedInput?.MaximumSpending);
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
