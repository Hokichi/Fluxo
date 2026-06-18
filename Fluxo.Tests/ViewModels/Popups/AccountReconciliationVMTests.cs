using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class AccountReconciliationVMTests
{
    [Fact]
    public void Constructor_FiltersBnplAndSavingSources_AndSelectsPromptSource()
    {
        var sources = CreateSourceViewModels();
        var appData = Substitute.For<IAppDataService>();

        var vm = new AccountReconciliationVM(sources, sources[2], appData, () => Task.CompletedTask);

        Assert.Equal(
            [AccountType.Credit, AccountType.Checking, AccountType.Cash],
            vm.Accounts.Select(source => source.AccountType).ToArray());
        Assert.Equal(3, vm.SelectedAccount?.Id);
    }

    [Fact]
    public async Task SaveAsync_CreatesNeedExpenseWithBudgetReconciliationTag()
    {
        var sources = CreateSourceViewModels();
        var appData = Substitute.For<IAppDataService>();
        var persistedSource = new Account
        {
            Id = 3,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 500m
        };
        var reconciliationTag = new ExpenseTag
        {
            Id = 9,
            Name = SystemExpenseTags.BudgetReconciliationName,
            HexCode = SystemExpenseTags.BudgetReconciliationHexCode,
            IsSystemTag = true
        };

        Expense? savedExpense = null;
        ExpenseLog? savedLog = null;
        var reloadCount = 0;

        appData.GetAccountByIdAsync(3, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Account?>(persistedSource));
        appData.GetExpenseTagsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseTag>>([reconciliationTag]));
        appData.AddExpenseAsync(Arg.Do<Expense>(expense => savedExpense = expense), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        appData.AddExpenseLogAsync(Arg.Do<ExpenseLog>(log => savedLog = log), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        appData.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var vm = new AccountReconciliationVM(sources, sources[2], appData, () =>
        {
            reloadCount++;
            return Task.CompletedTask;
        })
        {
            AmountText = 42.50m
        };

        var result = await vm.SaveAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CreatedExpenseLog);
        Assert.NotNull(savedExpense);
        Assert.NotNull(savedLog);
        Assert.Equal("Checking - Budget Reconciliation", savedExpense.Name);
        Assert.Equal(42.50m, savedExpense.Amount);
        Assert.Equal(ExpenseCategory.Needs, savedExpense.ExpenseCategory);
        Assert.Equal(3, savedExpense.AccountId);
        Assert.Equal(9, savedExpense.ExpenseTagId);
        Assert.Equal(42.50m, savedLog.Amount);
        Assert.Equal(DateTime.Today, savedLog.DeductedOn);
        Assert.Equal(3, savedLog.AccountId);
        Assert.False(savedLog.IsForDeletion);
        Assert.Equal(457.50m, persistedSource.Balance);
        Assert.Equal(1, reloadCount);
        appData.Received(1).UpdateAccount(persistedSource);
        await appData.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_AddsMissingBudgetReconciliationSystemTag()
    {
        var sources = CreateSourceViewModels();
        var appData = Substitute.For<IAppDataService>();
        var persistedSource = new Account
        {
            Id = 3,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 500m
        };
        ExpenseTag? addedTag = null;

        appData.GetAccountByIdAsync(3, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Account?>(persistedSource));
        appData.GetExpenseTagsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseTag>>([]));
        appData.AddExpenseTagAsync(Arg.Do<ExpenseTag>(tag =>
            {
                tag.Id = 14;
                addedTag = tag;
            }), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        appData.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var vm = new AccountReconciliationVM(sources, sources[2], appData, () => Task.CompletedTask)
        {
            AmountText = 12m
        };

        var result = await vm.SaveAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(addedTag);
        Assert.Equal(SystemExpenseTags.BudgetReconciliationName, addedTag.Name);
        Assert.Equal(SystemExpenseTags.BudgetReconciliationHexCode, addedTag.HexCode);
        Assert.True(addedTag.IsSystemTag);
        await appData.Received(1).AddExpenseTagAsync(Arg.Any<ExpenseTag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CanSave_IsFalseUntilAmountIsGreaterThanZero()
    {
        var sources = CreateSourceViewModels();
        var appData = Substitute.For<IAppDataService>();
        var changedProperties = new List<string?>();
        var vm = new AccountReconciliationVM(sources, sources[2], appData, () => Task.CompletedTask);
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert.False(vm.CanSave);

        vm.AmountText = 1m;

        Assert.True(vm.CanSave);
        Assert.Contains(nameof(AccountReconciliationVM.CanSave), changedProperties);

        vm.AmountText = 0m;

        Assert.False(vm.CanSave);
    }

    private static IReadOnlyList<AccountVM> CreateSourceViewModels()
    {
        return
        [
            new AccountVM { Id = 1, Name = "Credit", AccountType = AccountType.Credit },
            new AccountVM { Id = 2, Name = "Pay Later", AccountType = AccountType.BNPL },
            new AccountVM { Id = 3, Name = "Checking", AccountType = AccountType.Checking },
            new AccountVM { Id = 4, Name = "Cash", AccountType = AccountType.Cash },
            new AccountVM { Id = 5, Name = "Emergency", AccountType = AccountType.Saving }
        ];
    }
}
