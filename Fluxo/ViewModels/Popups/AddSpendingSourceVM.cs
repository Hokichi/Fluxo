using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Services.History;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class AddSpendingSourceVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;

    [ObservableProperty] private string _accountLimitText = string.Empty;
    [ObservableProperty] private string _apyText = string.Empty;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _primaryAmountText = string.Empty;
    [ObservableProperty] private SpendingSourceType _selectedSpendingSourceType = SpendingSourceType.Checking;
    [ObservableProperty] private bool _showOnUI = true;
    [ObservableProperty] private string _spentAmountText = string.Empty;

    public AddSpendingSourceVM(MainVM mainViewModel, Func<IUnitOfWork> unitOfWorkFactory)
    {
        _mainViewModel = mainViewModel;
        _unitOfWorkFactory = unitOfWorkFactory;
        DueDate = DateTime.Today.AddDays(14);
    }

    public IReadOnlyList<SpendingSourceTypeOption> SpendingSourceTypes { get; } =
    [
        new("Checking", SpendingSourceType.Checking),
        new("Cash", SpendingSourceType.Cash),
        new("Credit", SpendingSourceType.Credit),
        new("BNPL", SpendingSourceType.BNPL),
        new("Savings", SpendingSourceType.Saving)
    ];

    public bool IsCredit => SelectedSpendingSourceType == SpendingSourceType.Credit;
    public bool IsBnpl => SelectedSpendingSourceType == SpendingSourceType.BNPL;
    public bool IsCreditLike => IsCredit || IsBnpl;
    public bool IsSaving => SelectedSpendingSourceType == SpendingSourceType.Saving;
    public bool IsCashLike => SelectedSpendingSourceType is SpendingSourceType.Checking or SpendingSourceType.Cash;
    public string PrimaryAmountLabel => IsCreditLike ? "Current spent" : "Current balance";

    partial void OnSelectedSpendingSourceTypeChanged(SpendingSourceType value)
    {
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(IsBnpl));
        OnPropertyChanged(nameof(IsCreditLike));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(IsCashLike));
        OnPropertyChanged(nameof(PrimaryAmountLabel));

        if (!IsCreditLike)
        {
            AccountLimitText = string.Empty;
            SpentAmountText = string.Empty;
            DueDate = null;
        }

        if (!IsSaving)
            ApyText = string.Empty;
    }

    public async Task<AddSpendingSourceResult> SaveAsync()
    {
        if (IsBusy)
            return AddSpendingSourceResult.Failure("A source is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return AddSpendingSourceResult.Failure(validationMessage);

        IsBusy = true;

        try
        {
            await using var unitOfWork = _unitOfWorkFactory();

            var existingSources = await unitOfWork.SpendingSources.GetAllAsync();
            if (existingSources.Any(source =>
                    string.Equals(source.Name, input.Name, StringComparison.OrdinalIgnoreCase)))
                return AddSpendingSourceResult.Failure(
                    $"A spending source named \"{input.Name}\" already exists.");

            var spendingSource = new SpendingSource
            {
                Name = input.Name,
                SpendingSourceType = input.SpendingSourceType,
                AccountLimit = input.AccountLimit,
                SpentAmount = input.SpentAmount,
                Balance = input.Balance,
                DueDate = input.DueDate,
                InterestRate = input.InterestRate,
                ShowOnUI = input.ShowOnUI,
                IsEnabled = input.IsEnabled
            };

            await unitOfWork.SpendingSources.AddAsync(spendingSource);
            await unitOfWork.SaveChangesAsync();

            WeakReferenceMessenger.Default.Send(
                new RecordLogMemoryMessage(new AddSpendingSourceMemoryAction(
                    SpendingSourceMemorySnapshot.Create(spendingSource))));

            await _mainViewModel.ReloadCurrentDataAsync();
            return AddSpendingSourceResult.Success(true);
        }
        catch (Exception exception)
        {
            return AddSpendingSourceResult.Failure(
                $"Unable to create this spending source.\n\n{exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildInput(out AddSpendingSourceInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        var name = (NameText ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            validationMessage = "Please enter a source name.";
            return false;
        }

        if (!TryParseDecimal(PrimaryAmountText, out var primaryAmount))
        {
            validationMessage = $"{PrimaryAmountLabel} must be a valid amount.";
            return false;
        }

        if (!TryParseDecimal(SpentAmountText, out var spentAmount))
        {
            validationMessage = "Current spent must be a valid amount.";
            return false;
        }

        if (!TryParseDecimal(AccountLimitText, out var accountLimit))
        {
            validationMessage = "Account limit must be a valid amount.";
            return false;
        }

        decimal? interestRate = null;
        if (!string.IsNullOrWhiteSpace(ApyText))
        {
            if (!TryParseDecimal(ApyText, out var parsedApy))
            {
                validationMessage = "APY must be a valid amount.";
                return false;
            }

            interestRate = parsedApy;
        }

        if (primaryAmount < 0m || spentAmount < 0m || accountLimit < 0m || interestRate < 0m)
        {
            validationMessage = "Values must be zero or greater.";
            return false;
        }

        if (SelectedSpendingSourceType == SpendingSourceType.Credit && accountLimit <= 0m)
        {
            validationMessage = "Credit sources require an account limit greater than zero.";
            return false;
        }

        if (IsCreditLike && DueDate is null)
        {
            validationMessage = "Credit and BNPL sources require a due date.";
            return false;
        }

        input = new AddSpendingSourceInput(
            name,
            SelectedSpendingSourceType,
            IsCreditLike ? 0m : primaryAmount,
            IsCreditLike ? spentAmount : 0m,
            IsCredit ? accountLimit : 0m,
            IsCreditLike ? DueDate?.Date : null,
            IsSaving ? interestRate : null,
            ShowOnUI,
            IsEnabled);

        return true;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        value = 0m;
        var normalizedText = (text ?? string.Empty)
            .Trim()
            .Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalizedText))
            return true;

        return decimal.TryParse(normalizedText, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.CurrentCulture, out value) ||
               decimal.TryParse(normalizedText, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.InvariantCulture, out value);
    }

    public readonly record struct AddSpendingSourceResult(bool IsSuccess, bool ShouldClose, string? ErrorMessage)
    {
        public static AddSpendingSourceResult Success(bool shouldClose = false)
        {
            return new AddSpendingSourceResult(true, shouldClose, null);
        }

        public static AddSpendingSourceResult Failure(string? errorMessage)
        {
            return new AddSpendingSourceResult(false, false, errorMessage);
        }
    }

    public readonly record struct SpendingSourceTypeOption(string Label, SpendingSourceType Value);

    private readonly record struct AddSpendingSourceInput(
        string Name,
        SpendingSourceType SpendingSourceType,
        decimal Balance,
        decimal SpentAmount,
        decimal AccountLimit,
        DateTime? DueDate,
        decimal? InterestRate,
        bool ShowOnUI,
        bool IsEnabled);
}