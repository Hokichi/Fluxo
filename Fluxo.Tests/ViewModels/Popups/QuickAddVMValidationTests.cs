using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class QuickAddVMValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expense_Name_IsRequired(bool isRecurring)
    {
        RunInSta(() =>
        {
            var vm = CreateVm(TransactionKind.Expense, CreateCheckingSource(balance: 500m), isRecurring: isRecurring);
            vm.NameText = " ";

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Please enter a name.", result.ErrorMessage);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Income_Name_IsRequired(bool isRecurring)
    {
        RunInSta(() =>
        {
            var vm = CreateVm(TransactionKind.Income, CreateCheckingSource(balance: 0m), isRecurring: isRecurring);
            vm.NameText = " ";

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Please enter a name.", result.ErrorMessage);
        });
    }

    [Fact]
    public void GoalUpdate_Name_IsNotRequired()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(TransactionKind.Goal, CreateCheckingSource(balance: 500m), isRecurring: false);
            vm.NameText = " ";

            Assert.False(vm.HasErrors);
            Assert.True(vm.CanSave);
        });
    }

    [Fact]
    public void Constructor_DoesNotValidateNameOrAmountBeforeFieldsLoseFocus()
    {
        RunInSta(() =>
        {
            var vm = new QuickAddVM(CreateMainViewModel([CreateCheckingSource(balance: 500m)]), CreateAppData());

            Assert.False(vm.HasErrors);
            Assert.Equal(string.Empty, vm.NameValidationHint);
            Assert.Equal(string.Empty, vm.AmountValidationHint);
        });
    }

    [Fact]
    public void ValidateAmountField_DoesNotValidateNameField()
    {
        RunInSta(() =>
        {
            var vm = new QuickAddVM(CreateMainViewModel([CreateCheckingSource(balance: 500m)]), CreateAppData());

            vm.ValidateAmountField();

            Assert.Empty(vm.GetErrors(nameof(QuickAddVM.NameText)));
            Assert.Contains(vm.GetErrors(nameof(QuickAddVM.AmountText)), error => error.ErrorMessage == "Please enter a valid amount greater than zero.");
            Assert.Equal(string.Empty, vm.NameValidationHint);
            Assert.Equal("Invalid Amount", vm.AmountValidationHint);
        });
    }

    [Fact]
    public void ActivateAmountValidation_ActivatesOnceAndAmountChangesRevalidate()
    {
        RunInSta(() =>
        {
            var vm = new QuickAddVM(CreateMainViewModel([CreateCheckingSource(balance: 500m)]), CreateAppData());
            var amountErrorsChangedCount = 0;
            vm.ErrorsChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(QuickAddVM.AmountText))
                    amountErrorsChangedCount++;
            };

            vm.ActivateAmountValidation();
            var countAfterActivation = amountErrorsChangedCount;

            Assert.Contains(vm.GetErrors(nameof(QuickAddVM.AmountText)), error => error.ErrorMessage == "Please enter a valid amount greater than zero.");
            Assert.Equal("Invalid Amount", vm.AmountValidationHint);

            vm.ActivateAmountValidation();
            Assert.Equal(countAfterActivation, amountErrorsChangedCount);

            vm.AmountText = 10m;

            Assert.Empty(vm.GetErrors(nameof(QuickAddVM.AmountText)));
            Assert.Equal(string.Empty, vm.AmountValidationHint);
        });
    }

    [Fact]
    public void ValidateNameField_DoesNotValidateAmountField()
    {
        RunInSta(() =>
        {
            var vm = new QuickAddVM(CreateMainViewModel([CreateCheckingSource(balance: 500m)]), CreateAppData());

            vm.ValidateNameField();

            Assert.Contains(vm.GetErrors(nameof(QuickAddVM.NameText)), error => error.ErrorMessage == "Please enter a name.");
            Assert.Empty(vm.GetErrors(nameof(QuickAddVM.AmountText)));
            Assert.Equal("Required", vm.NameValidationHint);
            Assert.Equal(string.Empty, vm.AmountValidationHint);
        });
    }

    [Fact]
    public void TransactionModeChange_ClearsNameValidation()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(TransactionKind.Expense, CreateCheckingSource(balance: 500m), isRecurring: false);
            vm.NameText = " ";
            vm.ValidateNameField();

            Assert.True(vm.HasErrors);
            Assert.Equal("Required", vm.NameValidationHint);

            vm.IsIncome = true;

            Assert.Empty(vm.GetErrors(nameof(QuickAddVM.NameText)));
            Assert.Equal(string.Empty, vm.NameValidationHint);
        });
    }

    [Fact]
    public void AmountValidation_WhenActive_RevalidatesWhenSpendingSourceChanges()
    {
        RunInSta(() =>
        {
            var lowBalance = CreateCheckingSource(balance: 20m);
            var highBalance = new SpendingSourceVM
            {
                Id = 2,
                Name = "Checking 2",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 100m,
                IsEnabled = true
            };
            var vm = new QuickAddVM(CreateMainViewModel([lowBalance, highBalance]), CreateAppData());
            vm.SelectedSpendingSource = lowBalance;
            vm.AmountText = 30m;
            vm.ValidateAmountField();

            Assert.Equal("Insufficient Balance", vm.AmountValidationHint);

            vm.SelectedSpendingSource = highBalance;

            Assert.Empty(vm.GetErrors(nameof(QuickAddVM.AmountText)));
            Assert.Equal(string.Empty, vm.AmountValidationHint);
        });
    }

    [Fact]
    public void HasChanges_TracksOnlyNameAndAmountInput()
    {
        RunInSta(() =>
        {
            var vm = new QuickAddVM(CreateMainViewModel([CreateCheckingSource(balance: 500m)]), CreateAppData());
            vm.BeginChangeTracking();

            vm.SelectedDate = vm.SelectedDate.AddDays(1);
            vm.NoteText = "Ignore for pending updates";
            vm.IsRecurring = true;

            Assert.False(vm.HasChanges);

            vm.NameText = "Coffee";

            Assert.True(vm.HasChanges);
        });
    }

    [Fact]
    public void AmountValidationHint_MapsSpendingCapacityFailuresToShortText()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(TransactionKind.Expense, CreateCheckingSource(balance: 20m), isRecurring: false, amount: 30m);

            vm.ValidateAmountField();

            Assert.Equal("Insufficient Balance", vm.AmountValidationHint);
        });
    }

    [Theory]
    [InlineData(TransactionKind.Expense)]
    [InlineData(TransactionKind.Income)]
    public void Name_RejectsControlCharacters(TransactionKind kind)
    {
        RunInSta(() =>
        {
            var vm = CreateVm(kind, CreateCheckingSource(balance: 500m), isRecurring: false);
            vm.NameText = "Bad\u0001Name";

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Name cannot contain control characters.", result.ErrorMessage);
        });
    }

    [Theory]
    [InlineData(TransactionKind.Expense)]
    [InlineData(TransactionKind.Income)]
    public void Name_RejectsLengthOver256(TransactionKind kind)
    {
        RunInSta(() =>
        {
            var vm = CreateVm(kind, CreateCheckingSource(balance: 500m), isRecurring: false);
            vm.NameText = new string('a', 257);

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Name cannot exceed 256 characters.", result.ErrorMessage);
        });
    }

    [Theory]
    [InlineData(TransactionKind.Expense, false)]
    [InlineData(TransactionKind.Expense, true)]
    [InlineData(TransactionKind.Income, false)]
    [InlineData(TransactionKind.Income, true)]
    [InlineData(TransactionKind.Goal, false)]
    [InlineData(TransactionKind.Goal, true)]
    public void Amount_Zero_IsInvalid_ForAllTypes(TransactionKind kind, bool isRecurring)
    {
        RunInSta(() =>
        {
            var vm = CreateVm(kind, CreateCheckingSource(balance: 500m), isRecurring: isRecurring, amount: 0m);

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Please enter a valid amount greater than zero.", result.ErrorMessage);
        });
    }

    [Fact]
    public void SelectedSource_IsRequired()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(TransactionKind.Income, CreateCheckingSource(balance: 500m), isRecurring: false, amount: 10m);
            vm.SelectedSpendingSource = null;

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Please choose a spending source.", result.ErrorMessage);
        });
    }

    [Fact]
    public void SelectedTag_IsRequired_ForExpense()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(TransactionKind.Expense, CreateCheckingSource(balance: 500m), isRecurring: false, amount: 10m);
            vm.SelectedTag = null;

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Please choose a tag.", result.ErrorMessage);
        });
    }

    [Fact]
    public void SelectedGoal_IsRequired_ForGoalUpdate()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(TransactionKind.Goal, CreateCheckingSource(balance: 500m), isRecurring: false, amount: 10m);
            vm.SelectedGoal = null;

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Please choose a goal.", result.ErrorMessage);
        });
    }

    [Theory]
    [InlineData(TransactionKind.Expense, false)]
    [InlineData(TransactionKind.Expense, true)]
    [InlineData(TransactionKind.Goal, false)]
    [InlineData(TransactionKind.Goal, true)]
    public void Spending_OverflowOverMaximumSpending_IsInvalid(TransactionKind kind, bool isRecurring)
    {
        RunInSta(() =>
        {
            var source = CreateCheckingSource(balance: 500m, maximumSpending: 100m, moneyOut: 95m);
            var vm = CreateVm(kind, source, isRecurring: isRecurring, amount: 10m);

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Amount exceeds this source's maximum spending limit.", result.ErrorMessage);
        });
    }

    [Theory]
    [InlineData(TransactionKind.Expense)]
    [InlineData(TransactionKind.Goal)]
    public void Spending_OverflowOverBalance_IsInvalid(TransactionKind kind)
    {
        RunInSta(() =>
        {
            var vm = CreateVm(kind, CreateCheckingSource(balance: 20m), isRecurring: false, amount: 30m);

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Amount exceeds this source's available balance.", result.ErrorMessage);
        });
    }

    [Theory]
    [InlineData(TransactionKind.Expense)]
    public void Spending_OverflowOverAccountLimit_IsInvalid(TransactionKind kind)
    {
        RunInSta(() =>
        {
            var source = CreateCreditSource(accountLimit: 100m, spentAmount: 95m);
            var vm = CreateVm(kind, source, isRecurring: false, amount: 10m);

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Amount exceeds this source's account limit.", result.ErrorMessage);
        });
    }

    [Fact]
    public void Income_IsExempt_FromSpendingOverflowChecks()
    {
        RunInSta(() =>
        {
            var source = CreateCheckingSource(balance: 1m, maximumSpending: 1m, moneyOut: 1m);
            var vm = CreateVm(TransactionKind.Income, source, isRecurring: false, amount: 999m);

            Assert.False(vm.HasErrors);
            Assert.True(vm.CanSave);
        });
    }

    [Fact]
    public void RecurringTime_UsesSameValidationFlow()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(
                TransactionKind.Expense,
                CreateCheckingSource(balance: 500m),
                isRecurring: true,
                amount: 10m,
                recurringPeriod: RecurringPeriod.Weekly,
                recurringTimeText: "8");

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Recurring weekday must be between Monday and Sunday.", result.ErrorMessage);
        });
    }

    [Fact]
    public void SaveAsync_KeepsStaleSpendingCapacityCheck()
    {
        RunInSta(() =>
        {
            var sourceVm = CreateCheckingSource(balance: 200m);
            var staleSource = new SpendingSource
            {
                Id = sourceVm.Id,
                Name = sourceVm.Name,
                SpendingSourceType = sourceVm.SpendingSourceType,
                Balance = 10m,
                AccountLimit = 0m,
                MaximumSpending = 0m,
                SpentAmount = 0m,
                IsEnabled = true
            };

            var appData = CreateAppData();
            appData.GetSpendingSourceByIdAsync(sourceVm.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<SpendingSource?>(staleSource));

            var vm = CreateVm(TransactionKind.Expense, sourceVm, isRecurring: false, amount: 25m, appData: appData);
            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Amount exceeds this source's available balance.", result.ErrorMessage);
        });
    }

    [Fact]
    public void SaveAsync_KeepsStaleMaximumSpendingCheck_ForNonCreditSource()
    {
        RunInSta(() =>
        {
            var sourceVm = CreateCheckingSource(balance: 500m, maximumSpending: 100m, moneyOut: 0m);
            var staleSource = new SpendingSource
            {
                Id = sourceVm.Id,
                Name = sourceVm.Name,
                SpendingSourceType = sourceVm.SpendingSourceType,
                Balance = sourceVm.Balance,
                AccountLimit = 0m,
                MaximumSpending = 100m,
                SpentAmount = 95m,
                IsEnabled = true
            };

            var appData = CreateAppData();
            appData.GetSpendingSourceByIdAsync(sourceVm.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<SpendingSource?>(staleSource));

            var vm = CreateVm(TransactionKind.Expense, sourceVm, isRecurring: false, amount: 10m, appData: appData);
            Assert.False(vm.HasErrors);
            Assert.True(vm.CanSave);

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Amount exceeds this source's maximum spending limit.", result.ErrorMessage);
        });
    }

    [Fact]
    public void Constructor_UsesSpendingSourcesOverride_WhenProvided()
    {
        RunInSta(() =>
        {
            var persistedSource = CreateCheckingSource(balance: 500m);
            var temporarySource = new SpendingSourceVM
            {
                Id = -11,
                Name = "Wizard Temporary",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 250m,
                IsEnabled = true
            };

            var vm = new QuickAddVM(
                CreateMainViewModel([persistedSource]),
                CreateAppData(),
                spendingSourcesOverride: [temporarySource]);

            Assert.Single(vm.SpendingSources);
            Assert.Equal(-11, vm.SpendingSources.Single().Id);
            Assert.Equal("Wizard Temporary", vm.SpendingSources.Single().Name);
        });
    }

    [Fact]
    public void SaveAsync_RecurringDraftMode_UsesDraftCallback_ForTemporarySource()
    {
        RunInSta(() =>
        {
            var temporarySource = new SpendingSourceVM
            {
                Id = -7,
                Name = "Wizard Temporary",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 500m,
                IsEnabled = true
            };

            var appData = CreateAppData();
            appData.GetSpendingSourceByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<SpendingSource?>(null));

            QuickAddVM.RecurringDraftSaveInput? captured = null;
            var vm = new QuickAddVM(
                CreateMainViewModel([CreateCheckingSource(balance: 100m)]),
                appData,
                spendingSourcesOverride: [temporarySource],
                saveRecurringDraftAsync: input =>
                {
                    captured = input;
                    return Task.FromResult(QuickAddVM.QuickAddSubmissionResult.Success());
                });

            vm.IsExpense = true;
            vm.IsRecurring = true;
            vm.NameText = "Rent";
            vm.AmountText = 50m;
            vm.SelectedRecurringPeriod = RecurringPeriod.Monthly;
            vm.RecurringTimeText = "10";
            vm.SelectedSpendingSource = vm.SpendingSources.Single(source => source.Id == -7);

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.True(captured.HasValue);
            Assert.Equal(RecurringTransactionType.Expense, captured.Value.Type);
            Assert.Equal(-7, captured.Value.SpendingSourceId);
            Assert.Equal(50m, captured.Value.Amount);
            appData.DidNotReceiveWithAnyArgs().GetSpendingSourceByIdAsync(default, default);
        });
    }

    [Fact]
    public void RecurringDraft_HardStopExhaustedCategory_StillSavesDraft()
    {
        RunInSta(() =>
        {
            var source = CreateCheckingSource(balance: 500m);
            var allocation = new BudgetAllocation
            {
                AllocationLimit = 100m,
                AllocationPeriod = AllocationPeriod.Monthly,
                NeedsThreshold = 50,
                WantsThreshold = 30,
                InvestThreshold = 20,
                OverspendPolicy = OverspendPolicy.HardStop
            };
            var appData = CreateAppData(allocation, CreateExpenseLogsForBudget(ExpenseCategory.Wants, 30m));
            QuickAddVM.RecurringDraftSaveInput? captured = null;
            var vm = new QuickAddVM(
                CreateMainViewModel([source]),
                appData,
                saveRecurringDraftAsync: input =>
                {
                    captured = input;
                    return Task.FromResult(QuickAddVM.QuickAddSubmissionResult.Success());
                });
            vm.IsExpense = true;
            vm.IsRecurring = true;
            vm.NameText = "Subscription";
            vm.AmountText = 5m;
            vm.SelectedExpenseCategory = ExpenseCategory.Wants;
            vm.SelectedRecurringPeriod = RecurringPeriod.Monthly;
            vm.RecurringTimeText = "10";

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.True(captured.HasValue);
            Assert.Equal(RecurringTransactionType.Expense, captured.Value.Type);
            appData.DidNotReceive().UpdateBudgetAllocation(Arg.Any<BudgetAllocation>());
        });
    }

    [Fact]
    public void RecurringDraft_SoftDebt_DoesNotAddBudgetDebt()
    {
        RunInSta(() =>
        {
            var source = CreateCheckingSource(balance: 500m);
            var allocation = new BudgetAllocation
            {
                AllocationLimit = 100m,
                AllocationPeriod = AllocationPeriod.Monthly,
                NeedsThreshold = 50,
                WantsThreshold = 30,
                InvestThreshold = 20,
                OverspendPolicy = OverspendPolicy.SoftDebt
            };
            var appData = CreateAppData(allocation, CreateExpenseLogsForBudget(ExpenseCategory.Wants, 30m));
            var vm = new QuickAddVM(
                CreateMainViewModel([source]),
                appData,
                saveRecurringDraftAsync: _ => Task.FromResult(QuickAddVM.QuickAddSubmissionResult.Success()));
            vm.IsExpense = true;
            vm.IsRecurring = true;
            vm.NameText = "Subscription";
            vm.AmountText = 5m;
            vm.SelectedExpenseCategory = ExpenseCategory.Wants;
            vm.SelectedRecurringPeriod = RecurringPeriod.Monthly;
            vm.RecurringTimeText = "10";

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.Equal(0m, allocation.WantsDebt);
            appData.DidNotReceive().UpdateBudgetAllocation(Arg.Any<BudgetAllocation>());
        });
    }

    [Fact]
    public void Expense_HardStop_BlocksOverspendingCategory()
    {
        RunInSta(() =>
        {
            var source = CreateCheckingSource(balance: 500m);
            var allocation = new BudgetAllocation
            {
                AllocationLimit = 100m,
                AllocationPeriod = AllocationPeriod.Monthly,
                NeedsThreshold = 50,
                WantsThreshold = 30,
                InvestThreshold = 20,
                OverspendPolicy = OverspendPolicy.HardStop
            };
            var appData = CreateAppData(allocation, CreateExpenseLogsForBudget(ExpenseCategory.Wants, 25m));
            var vm = CreateVm(TransactionKind.Expense, source, isRecurring: false, amount: 6m, appData: appData);
            vm.SelectedExpenseCategory = ExpenseCategory.Wants;

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Wants budget is exhausted for this allocation period.", result.ErrorMessage);
            appData.DidNotReceiveWithAnyArgs().AddExpenseAsync(default!, default);
        });
    }

    [Fact]
    public void Expense_HardStop_UsesExpenseDateAllocationPeriod()
    {
        RunInSta(() =>
        {
            var source = CreateCheckingSource(balance: 500m);
            var allocation = new BudgetAllocation
            {
                AllocationLimit = 100m,
                AllocationPeriod = AllocationPeriod.Monthly,
                NeedsThreshold = 50,
                WantsThreshold = 30,
                InvestThreshold = 20,
                OverspendPolicy = OverspendPolicy.HardStop
            };
            var expenseDate = new DateTime(2026, 5, 15);
            var appData = CreateAppData(
                allocation,
                CreateExpenseLogsForBudget(ExpenseCategory.Wants, 29m, new DateTime(2026, 5, 1)));
            var vm = CreateVm(TransactionKind.Expense, source, isRecurring: false, amount: 2m, appData: appData);
            vm.SelectedExpenseCategory = ExpenseCategory.Wants;
            vm.SelectedDate = expenseDate;

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Wants budget is exhausted for this allocation period.", result.ErrorMessage);
        });
    }

    [Fact]
    public void Expense_SoftDebt_AddsCategoryDebt()
    {
        RunInSta(() =>
        {
            var source = CreateCheckingSource(balance: 500m);
            var allocation = new BudgetAllocation
            {
                AllocationLimit = 100m,
                AllocationPeriod = AllocationPeriod.Monthly,
                NeedsThreshold = 50,
                WantsThreshold = 30,
                InvestThreshold = 20,
                OverspendPolicy = OverspendPolicy.SoftDebt
            };
            var appData = CreateAppData(allocation, CreateExpenseLogsForBudget(ExpenseCategory.Wants, 25m));
            var vm = CreateVm(TransactionKind.Expense, source, isRecurring: false, amount: 12m, appData: appData);
            vm.SelectedExpenseCategory = ExpenseCategory.Wants;

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.Equal(7m, allocation.WantsDebt);
            appData.Received(1).UpdateBudgetAllocation(allocation);
        });
    }

    [Fact]
    public void GoalUpdate_HardStop_BlocksOverspendingInvestCategory()
    {
        RunInSta(() =>
        {
            var source = CreateCheckingSource(balance: 500m);
            var allocation = new BudgetAllocation
            {
                AllocationLimit = 100m,
                AllocationPeriod = AllocationPeriod.Monthly,
                NeedsThreshold = 50,
                WantsThreshold = 30,
                InvestThreshold = 20,
                OverspendPolicy = OverspendPolicy.HardStop
            };
            var appData = CreateAppData(allocation, CreateExpenseLogsForBudget(ExpenseCategory.Savings, 20m));
            var vm = CreateVm(TransactionKind.Goal, source, isRecurring: false, amount: 1m, appData: appData);

            var result = vm.SaveAsync(resetAfterSave: false).GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Invest budget is exhausted for this allocation period.", result.ErrorMessage);
            appData.DidNotReceiveWithAnyArgs().AddExpenseAsync(default!, default);
        });
    }

    private static QuickAddVM CreateVm(
        TransactionKind kind,
        SpendingSourceVM source,
        bool isRecurring,
        decimal amount = 10m,
        string name = "Valid name",
        RecurringPeriod recurringPeriod = RecurringPeriod.Monthly,
        string recurringTimeText = "1",
        IAppDataService? appData = null)
    {
        var main = CreateMainViewModel([source]);
        var data = appData ?? CreateAppData();
        var vm = new QuickAddVM(main, data);

        switch (kind)
        {
            case TransactionKind.Expense:
                vm.IsExpense = true;
                vm.IsGoal = false;
                break;
            case TransactionKind.Income:
                vm.IsIncome = true;
                break;
            case TransactionKind.Goal:
                vm.IsGoal = true;
                break;
        }

        vm.AmountText = amount;
        vm.NameText = name;
        vm.IsRecurring = isRecurring;
        vm.SelectedRecurringPeriod = recurringPeriod;
        vm.RecurringTimeText = recurringTimeText;
        return vm;
    }

    private static IAppDataService CreateAppData(
        BudgetAllocation? budgetAllocation = null,
        IReadOnlyList<ExpenseLog>? expenseLogs = null)
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLog>>(expenseLogs ?? []));
        appData.GetIncomeLogsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IncomeLog>>([]));
        appData.GetSpendingSourceByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult<SpendingSource?>(new SpendingSource
            {
                Id = callInfo.ArgAt<int>(0),
                Name = "Default",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 1_000m,
                AccountLimit = 0m,
                MaximumSpending = 0m,
                SpentAmount = 0m,
                IsEnabled = true
            }));
        appData.GetExpenseTagByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult<ExpenseTag?>(new ExpenseTag
            {
                Id = callInfo.ArgAt<int>(0),
                Name = "General",
                HexCode = "#22C55E",
                IsSystemTag = false
            }));
        appData.GetSavingGoalByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult<SavingGoal?>(new SavingGoal
            {
                Id = callInfo.ArgAt<int>(0),
                Name = "Goal",
                TargetAmount = 500m,
                CurrentAmount = 100m
            }));
        appData.GetExpenseTagsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseTag>>(
            [
                new ExpenseTag
                {
                    Id = 99,
                    Name = GoalUpdateTransactionSupport.GoalUpdateTagName,
                    HexCode = GoalUpdateTransactionSupport.GoalUpdateTagColor,
                    IsSystemTag = false
                }
            ]));
        appData.AddExpenseAsync(Arg.Any<Expense>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        appData.AddExpenseLogAsync(Arg.Any<ExpenseLog>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        appData.AddRecurringTransactionAsync(Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        appData.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        appData.GetBudgetAllocationAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(budgetAllocation ?? new BudgetAllocation()));
        return appData;
    }

    private static MainVM CreateMainViewModel(IReadOnlyList<SpendingSourceVM> spendingSources)
    {
        var messenger = new WeakReferenceMessenger();
        var mapper = Substitute.For<IMapper>();
        var unitOfWork = CreateUnitOfWork();
        var dataOperationRunner = new InlineDataOperationRunner(unitOfWork);
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<SpendingSourceVM>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<ExpenseTagVM>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<Fluxo.Core.DTO.RecurringTransactionDto>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<RecurringTransactionVM>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<Fluxo.Core.DTO.SavingGoalDto>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<SavingGoalVM>>(Arg.Any<object>()).Returns([]);

        var expenseLogService = Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Fluxo.Core.DTO.ExpenseLogDto>>([]));
        var spendingSourceService = Substitute.For<ISpendingSourceService>();
        spendingSourceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Fluxo.Core.DTO.SpendingSourceDto>>([]));
        var tagService = Substitute.For<ITagService>();
        tagService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Fluxo.Core.DTO.ExpenseTagDto>>([]));
        var expenseService = Substitute.For<IExpenseService>();
        expenseService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Fluxo.Core.DTO.ExpenseDto>>([]));

        var dashboard = new DashboardVM(
            new NotificationPanelVM(
                expenseService,
                expenseLogService,
                spendingSourceService,
                dataOperationRunner,
                mapper,
                messenger: messenger),
            new BudgetAllocationPanelVM(
                expenseLogService,
                spendingSourceService,
                tagService,
                dataOperationRunner,
                mapper,
                messenger),
            new SpentAllowancePanelVM(
                expenseLogService,
                spendingSourceService,
                dataOperationRunner,
                mapper,
                messenger),
            new SavingGoalsPanelVM(dataOperationRunner, mapper, messenger),
            new MainViewModeToggleVM(messenger));
        var main = new MainVM(
            dataOperationRunner,
            dashboard,
            new DaySpinnerVM(messenger),
            null);

        foreach (var source in spendingSources)
            main.BudgetPanel.SpendingSources.Add(source);

        main.BudgetPanel.Tags = new ObservableCollection<ExpenseTagVM>(
        [
            new ExpenseTagVM
            {
                Id = 1,
                Name = "General",
                HexCode = "#22C55E",
                IsSystemTag = false
            }
        ]);
        main.BudgetPanel.OtherTags = new ObservableCollection<ExpenseTagVM>();
        main.SavingGoalsPanel.SavingGoals.Add(new SavingGoalVM
        {
            Id = 1,
            Name = "Goal",
            TargetAmount = 500m,
            CurrentAmount = 100m
        });

        return main;
    }

    private static IUnitOfWork CreateUnitOfWork()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userSettings = Substitute.For<IUserSettingsRepository>();
        userSettings.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>([]));
        unitOfWork.UserSettings.Returns(userSettings);

        var incomeLogs = Substitute.For<IIncomeLogRepository>();
        incomeLogs.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IncomeLog>>([]));
        unitOfWork.IncomeLogs.Returns(incomeLogs);

        var budgetAllocation = Substitute.For<IBudgetAllocationRepository>();
        budgetAllocation.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BudgetAllocation?>(new BudgetAllocation()));
        unitOfWork.BudgetAllocation.Returns(budgetAllocation);

        return unitOfWork;
    }

    private static IReadOnlyList<ExpenseLog> CreateExpenseLogsForBudget(
        ExpenseCategory category,
        decimal amount,
        DateTime? deductedOn = null)
    {
        return
        [
            new ExpenseLog
            {
                Id = 10,
                Amount = amount,
                DeductedOn = deductedOn ?? DateTime.Today,
                IsForDeletion = false,
                Expense = new Expense
                {
                    Id = 20,
                    Name = "Existing",
                    ExpenseCategory = category
                }
            }
        ];
    }

    private static SpendingSourceVM CreateCheckingSource(
        decimal balance,
        decimal maximumSpending = 0m,
        decimal moneyOut = 0m)
    {
        return new SpendingSourceVM
        {
            Id = 1,
            Name = "Checking",
            SpendingSourceType = SpendingSourceType.Checking,
            Balance = balance,
            MaximumSpending = maximumSpending,
            MoneyOut = moneyOut,
            IsEnabled = true
        };
    }

    private static SpendingSourceVM CreateCreditSource(
        decimal accountLimit,
        decimal spentAmount,
        decimal maximumSpending = 0m)
    {
        return new SpendingSourceVM
        {
            Id = 1,
            Name = "Credit",
            SpendingSourceType = SpendingSourceType.Credit,
            AccountLimit = accountLimit,
            SpentAmount = spentAmount,
            MaximumSpending = maximumSpending,
            IsEnabled = true
        };
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public enum TransactionKind
    {
        Expense,
        Income,
        Goal
    }
}
