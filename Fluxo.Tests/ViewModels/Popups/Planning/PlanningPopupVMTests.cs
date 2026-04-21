using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Planning;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Planning;

public class PlanningPopupVMTests
{
    [Fact]
    public void ShouldPromptCloseOnMissingIncome_ReturnsTrueWhenNoIncomeOrTotalIsZero()
    {
        var vm = CreateVm([]);

        Assert.True(vm.ShouldPromptCloseOnMissingIncome());

        vm.Incomes.Add(new IncomeLogVM { Amount = 0m });

        Assert.True(vm.ShouldPromptCloseOnMissingIncome());

        vm.Incomes.Add(new IncomeLogVM { Amount = 125m });

        Assert.False(vm.ShouldPromptCloseOnMissingIncome());
        Assert.Equal(125m, vm.TotalIncome);
    }

    [Fact]
    public void IsAllocationValid_ReturnsFalseWhenAllocationDoesNotTotalOneHundred()
    {
        var vm = CreateVm([]);

        Assert.True(vm.IsAllocationValid);

        vm.NeedsPercent = 50;
        vm.WantsPercent = 25;
        vm.InvestPercent = 20;

        Assert.False(vm.IsAllocationValid);
    }

    [Fact]
    public async Task SetImportFixedExpensesAsync_LoadsFromDbOnceAndReusesCacheAfterToggle()
    {
        var expenseRepository = Substitute.For<IExpenseRepository>();
        expenseRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Expense>>([
                CreateExpense(1, ExpenseKind.Fixed, "Rent"),
                CreateExpense(2, ExpenseKind.Fixed, "Internet"),
                CreateExpense(3, ExpenseKind.Manual, "Lunch")
            ]));

        var vm = CreateVm([], expenseRepository);
        vm.Expenses.Add(new ExpenseVM
        {
            Id = 99,
            Name = "One-off",
            ExpenseKind = ExpenseKind.Manual,
            ExpenseCategory = ExpenseCategory.Wants
        });

        await vm.SetImportFixedExpensesAsync(true);
        await vm.SetImportFixedExpensesAsync(true);

        Assert.Equal(3, vm.Expenses.Count);
        Assert.Equal(2, vm.Expenses.Count(expense => expense.ExpenseKind == ExpenseKind.Fixed));

        await vm.SetImportFixedExpensesAsync(false);

        Assert.Single(vm.Expenses);
        Assert.Equal(99, vm.Expenses[0].Id);

        await vm.SetImportFixedExpensesAsync(true);

        Assert.Equal(3, vm.Expenses.Count);
        Assert.Equal(2, vm.Expenses.Count(expense => expense.ExpenseKind == ExpenseKind.Fixed));
        await expenseRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ClearingIncomes_RebuildsSubscriptionsWithoutLeakingRemovedHandlers()
    {
        var vm = CreateVm([]);
        var removedIncome = new IncomeLogVM { Amount = 100m };
        var retainedIncome = new IncomeLogVM { Amount = 50m };
        var totalIncomeNotifications = 0;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlanningPopupVM.TotalIncome))
                totalIncomeNotifications++;
        };

        vm.Incomes.Add(removedIncome);
        vm.Incomes.Clear();

        totalIncomeNotifications = 0;
        removedIncome.Amount = 200m;

        Assert.Equal(0, totalIncomeNotifications);
        Assert.Equal(0m, vm.TotalIncome);

        vm.Incomes.Add(retainedIncome);
        totalIncomeNotifications = 0;
        retainedIncome.Amount = 75m;

        Assert.Equal(1, totalIncomeNotifications);
        Assert.Equal(75m, vm.TotalIncome);
    }

    [Fact]
    public void PlanningSnapshot_DeepCopyKeepsOriginalGraphIndependent()
    {
        var snapshot = new PlanningSnapshot(
            [CreateIncome(1, 125m)],
            [CreateExpenseVm(2, ExpenseKind.Fixed, "Rent")],
            needsPercent: 40,
            wantsPercent: 30,
            investPercent: 30,
            cachedFixedExpenses: new Dictionary<int, ExpenseVM>
            {
                [2] = CreateExpenseVm(2, ExpenseKind.Fixed, "Rent")
            },
            importedFixedExpenseIds: [2]);

        var copy = snapshot.DeepCopy();

        copy.Incomes[0].Amount = 999m;
        copy.Expenses[0].Name = "Changed";
        copy.CachedFixedExpenses[2].Name = "Cached Changed";

        Assert.Equal(125m, snapshot.Incomes[0].Amount);
        Assert.Equal("Rent", snapshot.Expenses[0].Name);
        Assert.Equal("Rent", snapshot.CachedFixedExpenses[2].Name);
        Assert.Equal(2, snapshot.ImportedFixedExpenseIds.Single());
    }

    [Fact]
    public async Task BuildSnapshot_RoundTripsImportedFixedExpenseCacheAndState()
    {
        var expenseRepository = Substitute.For<IExpenseRepository>();
        var callCount = 0;
        expenseRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;

                if (callCount > 1)
                    throw new InvalidOperationException("Restored VM should reuse cached fixed expenses.");

                return Task.FromResult<IReadOnlyList<Expense>>([
                    CreateExpense(1, ExpenseKind.Fixed, "Rent")
                ]);
            });

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Expenses.Returns(expenseRepository);

        var vm = new PlanningPopupVM(unitOfWork);
        await vm.SetImportFixedExpensesAsync(true);

        var snapshot = vm.BuildSnapshot();
        var restoredVm = new PlanningPopupVM(unitOfWork, snapshot);

        Assert.Single(restoredVm.Expenses);
        Assert.Equal(1, restoredVm.Expenses[0].Id);

        await restoredVm.SetImportFixedExpensesAsync(false);
        Assert.Empty(restoredVm.Expenses);

        await restoredVm.SetImportFixedExpensesAsync(true);

        Assert.Single(restoredVm.Expenses);
        Assert.Equal(1, restoredVm.Expenses[0].Id);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task BuildSnapshot_RoundTripsLoadedEmptyFixedExpenseCacheWithoutExtraDbCall()
    {
        var expenseRepository = Substitute.For<IExpenseRepository>();
        var callCount = 0;
        expenseRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return Task.FromResult<IReadOnlyList<Expense>>([]);
            });

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Expenses.Returns(expenseRepository);

        var vm = new PlanningPopupVM(unitOfWork);
        await vm.SetImportFixedExpensesAsync(true);

        var snapshot = vm.BuildSnapshot();
        var restoredVm = new PlanningPopupVM(unitOfWork, snapshot);

        await restoredVm.SetImportFixedExpensesAsync(true);

        Assert.Empty(restoredVm.Expenses);
        Assert.Equal(1, callCount);
    }

    private static PlanningPopupVM CreateVm(
        IReadOnlyList<Expense> fixedExpenses,
        IExpenseRepository? expenseRepository = null)
    {
        if (expenseRepository is null)
        {
            expenseRepository = Substitute.For<IExpenseRepository>();
            expenseRepository.GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(fixedExpenses));
        }

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Expenses.Returns(expenseRepository);

        return new PlanningPopupVM(unitOfWork);
    }

    private static IncomeLogVM CreateIncome(int id, decimal amount)
    {
        return new IncomeLogVM
        {
            Id = id,
            Amount = amount,
            AddedOn = DateTime.UnixEpoch,
            Notes = $"Income {id}",
            SpendingSource = new SpendingSourceVM
            {
                Id = id,
                Name = $"Source {id}",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 500m,
                IsEnabled = true,
                ShowOnUI = true
            }
        };
    }

    private static Expense CreateExpense(int id, ExpenseKind kind, string name)
    {
        return new Expense
        {
            Id = id,
            Name = name,
            Amount = id * 100m,
            ExpenseKind = kind,
            ExpenseCategory = ExpenseCategory.Needs,
            RecurringDate = id,
            IsActive = true,
            ExpenseTag = new ExpenseTag
            {
                Id = id,
                Name = $"Tag {id}",
                HexCode = "#000000",
                IsSystemTag = false
            },
            SpendingSource = new SpendingSource
            {
                Id = id,
                Name = $"Source {id}",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 1000m,
                IsEnabled = true,
                ShowOnUI = true
            }
        };
    }

    private static ExpenseVM CreateExpenseVm(int id, ExpenseKind kind, string name)
    {
        return new ExpenseVM
        {
            Id = id,
            Name = name,
            Amount = id * 100m,
            ExpenseKind = kind,
            ExpenseCategory = ExpenseCategory.Needs,
            RecurringDate = id,
            IsActive = true,
            ExpenseTag = new ExpenseTagVM
            {
                Id = id,
                Name = $"Tag {id}",
                HexCode = "#000000",
                IsSystemTag = false
            },
            SpendingSource = new SpendingSourceVM
            {
                Id = id,
                Name = $"Source {id}",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 1000m,
                IsEnabled = true,
                ShowOnUI = true
            }
        };
    }
}
