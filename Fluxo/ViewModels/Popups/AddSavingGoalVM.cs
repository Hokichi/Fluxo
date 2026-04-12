using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class AddSavingGoalVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;

    [ObservableProperty] private string _currentAmountText = string.Empty;
    [ObservableProperty] private DateTime _endDate = DateTime.Today.AddMonths(3);
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _targetAmountText = string.Empty;

    public int? EditingId { get; init; }

    public AddSavingGoalVM(MainVM mainViewModel, Func<IUnitOfWork> unitOfWorkFactory)
    {
        _mainViewModel = mainViewModel;
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task<AddSavingGoalResult> SaveAsync()
    {
        if (IsBusy)
            return AddSavingGoalResult.Failure("A saving goal is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return AddSavingGoalResult.Failure(validationMessage);

        IsBusy = true;

        try
        {
            await using var unitOfWork = _unitOfWorkFactory();

            if (EditingId.HasValue)
            {
                var existing = await unitOfWork.SavingGoals.GetByIdAsync(EditingId.Value);
                if (existing is null)
                    return AddSavingGoalResult.Failure("Saving goal not found.");

                existing.Name = input.Name;
                existing.TargetAmount = input.TargetAmount;
                existing.CurrentAmount = input.CurrentAmount;
                existing.SavingEndDate = input.EndDate;
                unitOfWork.SavingGoals.Update(existing);
            }
            else
            {
                var savingGoal = new SavingGoal
                {
                    Name = input.Name,
                    TargetAmount = input.TargetAmount,
                    CurrentAmount = input.CurrentAmount,
                    SavingEndDate = input.EndDate
                };
                await unitOfWork.SavingGoals.AddAsync(savingGoal);
            }

            await unitOfWork.SaveChangesAsync();
            await _mainViewModel.ReloadCurrentDataAsync(true);

            return AddSavingGoalResult.Success(true);
        }
        catch (Exception exception)
        {
            return AddSavingGoalResult.Failure($"Unable to create this saving goal.\n\n{exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildInput(out AddSavingGoalInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        var name = (NameText ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            validationMessage = "Please enter a goal name.";
            return false;
        }

        if (!TryParseDecimal(TargetAmountText, out var targetAmount) || targetAmount <= 0m)
        {
            validationMessage = "Please enter a target amount greater than zero.";
            return false;
        }

        if (!TryParseDecimal(CurrentAmountText, out var currentAmount))
        {
            validationMessage = "Current amount must be a valid amount.";
            return false;
        }

        if (currentAmount < 0m)
        {
            validationMessage = "Current amount must be zero or greater.";
            return false;
        }

        input = new AddSavingGoalInput(name, targetAmount, currentAmount, EndDate.Date);
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

    public readonly record struct AddSavingGoalResult(bool IsSuccess, bool ShouldClose, string? ErrorMessage)
    {
        public static AddSavingGoalResult Success(bool shouldClose = false)
        {
            return new AddSavingGoalResult(true, shouldClose, null);
        }

        public static AddSavingGoalResult Failure(string? errorMessage)
        {
            return new AddSavingGoalResult(false, false, errorMessage);
        }
    }

    private readonly record struct AddSavingGoalInput(
        string Name,
        decimal TargetAmount,
        decimal CurrentAmount,
        DateTime EndDate);
}
