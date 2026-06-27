using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data;
using Fluxo.Data.Context;
using Fluxo.Data.Repositories;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class ExpenseDetailVMSplitTests
{
    [Fact]
    public void BeginSplitMode_IsAvailableFromReadOnlyAndDefaultsRowsFromParent()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();

            Assert.False(vm.IsEditing);

            vm.BeginSplitMode();
            vm.AddSplitRow();

            Assert.True(vm.IsSplitMode);
            Assert.True(vm.IsEditing);
            Assert.Single(vm.SplitRows);
            Assert.Equal(ExpenseCategory.Needs, vm.SplitRows[0].SelectedExpenseCategory);
            Assert.Equal(1, vm.SplitRows[0].SelectedTag?.Id);
        });
    }

    [Fact]
    public void SplitRow_SelectingTag_ClosesTagPopup()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.BeginSplitMode();
            vm.AddSplitRow();

            var row = vm.SplitRows[0];
            row.IsTagPopupOpen = true;
            row.SelectedTag = new TagVM { Id = 2, Name = "Travel", HexCode = "#3B82F6", IsSystemTag = false };

            Assert.False(row.IsTagPopupOpen);
        });
    }

    [Fact]
    public void BeginSplitMode_IsAvailableFromEditMode()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.BeginEditingAsync().GetAwaiter().GetResult();

            vm.BeginSplitMode();

            Assert.True(vm.IsSplitMode);
            Assert.True(vm.IsEditing);
        });
    }

    [Fact]
    public void BeginSplitModeAsync_LoadsExistingChildTransactionsIntoSplitRows()
    {
        RunInSta(() =>
        {
            var (vm, appData, persistedSource) = CreateVmWithDependencies(amount: 100m);
            appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ExpenseLog>>(
                [
                    new ExpenseLog
                    {
                        Id = 200,
                        ParentLogId = 10,
                        Amount = 35m,
                        DeductedOn = new DateTime(2026, 5, 31),
                        Notes = "Child note",
                        IsIoU = true,
                        Account = persistedSource,
                        Expense = new Expense
                        {
                            Name = "Groceries",
                            ExpenseCategory = ExpenseCategory.Wants,
                            Tag = new Tag { Id = 2, Name = "Travel", HexCode = "#3B82F6" },
                            IsIoU = true
                        }
                    },
                    new ExpenseLog
                    {
                        Id = 201,
                        ParentLogId = 99,
                        Amount = 15m,
                        DeductedOn = new DateTime(2026, 5, 31),
                        Account = persistedSource,
                        Expense = new Expense { Name = "Other parent" }
                    }
                ]));

            vm.BeginSplitModeAsync().GetAwaiter().GetResult();

            var row = Assert.Single(vm.SplitRows);
            Assert.Equal("Groceries", row.NameText);
            Assert.Equal(35m, row.AmountText);
            Assert.Equal(ExpenseCategory.Wants, row.SelectedExpenseCategory);
            Assert.Equal(2, row.SelectedTag?.Id);
            Assert.Equal("Travel", row.SelectedTag?.Name);
            Assert.True(row.IsIoU);
        });
    }

    [Fact]
    public void BeginSplitModeAsync_HidesChildTransactionsPanelAndUsesCompactWidth()
    {
        RunInSta(() =>
        {
            var (vm, appData, persistedSource) = CreateVmWithDependencies(amount: 100m);
            appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ExpenseLog>>(
                [
                    new ExpenseLog
                    {
                        Id = 200,
                        ParentLogId = 10,
                        Amount = 35m,
                        DeductedOn = new DateTime(2026, 5, 31),
                        Account = persistedSource,
                        Expense = new Expense { Name = "Groceries" }
                    }
                ]));

            vm.LoadChildTransactionsAsync().GetAwaiter().GetResult();

            Assert.True(vm.ShowChildTransactions);
            Assert.Equal(916, vm.DetailPopupWidth);

            vm.BeginSplitModeAsync().GetAwaiter().GetResult();

            Assert.False(vm.ShowChildTransactions);
            Assert.Equal(640, vm.DetailPopupWidth);
        });
    }

    [Fact]
    public void SaveAsync_NormalMode_PersistsPinnedState()
    {
        RunInSta(() =>
        {
            var (vm, appData, _) = CreateVmWithDependencies();
            vm.BeginEditingAsync().GetAwaiter().GetResult();
            vm.IsPinned = true;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            appData.Received(1).UpdateExpenseLog(Arg.Is<ExpenseLog>(log => log.Id == 10 && log.IsPinned));
        });
    }

    [Fact]
    public void LoadChildTransactionsAsync_LoadsChildrenForCurrentParentOnly()
    {
        RunInSta(() =>
        {
            var (vm, appData, persistedSource) = CreateVmWithDependencies();
            var tag = new Tag { Id = 1, Name = "General", HexCode = "#22C55E" };
            appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ExpenseLog>>(
                [
                    new ExpenseLog
                    {
                        Id = 200,
                        ParentLogId = 10,
                        Amount = 35m,
                        DeductedOn = new DateTime(2026, 5, 31),
                        Notes = "Child note",
                        IsIoU = true,
                        Account = persistedSource,
                        Expense = new Expense
                        {
                            Name = "Groceries",
                            ExpenseCategory = ExpenseCategory.Needs,
                            Tag = tag
                        }
                    },
                    new ExpenseLog
                    {
                        Id = 201,
                        ParentLogId = 99,
                        Amount = 15m,
                        DeductedOn = new DateTime(2026, 5, 31),
                        Account = persistedSource,
                        Expense = new Expense { Name = "Other parent", Tag = tag }
                    }
                ]));

            vm.LoadChildTransactionsAsync().GetAwaiter().GetResult();

            var child = Assert.Single(vm.ChildTransactions);
            Assert.True(vm.HasChildTransactions);
            Assert.Equal(916, vm.DetailPopupWidth);
            Assert.Equal("Groceries", child.Name);
            Assert.Equal(35m, child.Amount);
            Assert.Equal("General", child.TagName);
            Assert.True(child.IsIoU);
        });
    }

    [Fact]
    public void DeleteAsync_RemovesExpenseLogAndRecordsHistory()
    {
        RunInSta(() =>
        {
            var recipient = new MessageCaptureRecipient();
            WeakReferenceMessenger.Default.Register<MessageCaptureRecipient, RecordLogMemoryMessage>(
                recipient,
                static (target, message) => target.RecordLogMemoryMessages.Add(message));

            try
            {
                var (vm, appData, persistedSource) = CreateVmWithDependencies(amount: 100m);

                var result = vm.DeleteAsync().GetAwaiter().GetResult();

                Assert.True(result.IsSuccess);
                Assert.Equal(1_100m, persistedSource.Balance);
                appData.Received(1).RemoveExpenseLog(Arg.Is<ExpenseLog>(log => log.Id == 10));
                appData.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
                Assert.Contains(
                    recipient.RecordLogMemoryMessages,
                    message => message.Value is DeleteExpenseLogMemoryAction action &&
                               action.Snapshot?.ExpenseLogId == 10);
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(recipient);
            }
        });
    }

    [Fact]
    public void CancelEditing_FromSplitMode_RestoresOriginalAmountAndLeavesSplitMode()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].AmountText = 25m;
            vm.SplitRows[0].AmountText = 30m;

            Assert.Equal(70m, vm.AmountText);

            vm.CancelEditing();

            Assert.False(vm.IsSplitMode);
            Assert.Empty(vm.SplitRows);
            Assert.Equal(100m, vm.AmountText);
        });
    }

    [Fact]
    public void SplitRows_RecalculateRemainderAndMarkCausingRowWhenNegative()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].AmountText = 60m;
            vm.AddSplitRow();

            var causingRow = vm.SplitRows[1];
            causingRow.AmountText = 50m;

            Assert.Equal(-10m, vm.AmountText);
            Assert.True(vm.HasNegativeSplitRemainder);
            Assert.Same(causingRow, vm.NegativeRemainderRow);
            Assert.False(vm.SplitRows[0].IsCausingNegativeRemainder);
            Assert.True(causingRow.IsCausingNegativeRemainder);
        });
    }

    [Fact]
    public void SplitMode_ManualParentAmountEdit_UpdatesNegativeState()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].AmountText = 120m;

            Assert.True(vm.HasNegativeSplitRemainder);
            Assert.True(vm.SplitRows[0].IsCausingNegativeRemainder);

            vm.AmountText = 10m;

            Assert.False(vm.HasNegativeSplitRemainder);
            Assert.Null(vm.NegativeRemainderRow);
            Assert.False(vm.SplitRows[0].IsCausingNegativeRemainder);

            vm.AmountText = -5m;

            Assert.True(vm.HasNegativeSplitRemainder);
            Assert.Same(vm.SplitRows[0], vm.NegativeRemainderRow);
            Assert.True(vm.SplitRows[0].IsCausingNegativeRemainder);
        });
    }

    [Fact]
    public void SplitMode_CloseState_DistinguishesNoRowsFromRowsWithoutAmounts()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            vm.BeginSplitMode();

            Assert.True(vm.CanCloseSplitModeWithoutSaving);
            Assert.False(vm.RequiresEmptySplitConfirmationOnClose);

            vm.AddSplitRow();
            vm.SplitRows[0].NameText = "Pending row";

            Assert.False(vm.CanCloseSplitModeWithoutSaving);
            Assert.True(vm.RequiresEmptySplitConfirmationOnClose);
        });
    }

    [Fact]
    public void SplitMode_CloseState_PropertiesRaiseNotifications()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            var changedProperties = new List<string>();
            vm.PropertyChanged += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.PropertyName))
                    changedProperties.Add(eventArgs.PropertyName);
            };

            vm.BeginSplitMode();
            vm.AddSplitRow();

            Assert.Contains(nameof(ExpenseDetailVM.CanCloseSplitModeWithoutSaving), changedProperties);
            Assert.Contains(nameof(ExpenseDetailVM.RequiresEmptySplitConfirmationOnClose), changedProperties);
        });
    }

    [Fact]
    public void SaveAsync_FromSplitModeWithNoChanges_ClearsSplitState()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].AmountText = 25m;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.False(vm.IsSplitMode);
            Assert.Empty(vm.SplitRows);
        });
    }

    [Fact]
    public void SaveAsync_FromSplitModeWithChanges_ClearsSplitState()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].AmountText = 25m;
            vm.NameText = "Updated expense";

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.False(vm.IsSplitMode);
            Assert.Empty(vm.SplitRows);
        });
    }

    [Fact]
    public void SaveAsync_FromExistingSplitMode_UpdatesChildrenInsteadOfDuplicating()
    {
        RunInSta(() =>
        {
            var (vm, appData, persistedSource) = CreateVmWithDependencies(amount: 100m);
            var childTag = new Tag { Id = 2, Name = "Travel", HexCode = "#3B82F6" };
            var childExpense = new Expense
            {
                Id = 30,
                Name = "Old child",
                Amount = 25m,
                ExpenseCategory = ExpenseCategory.Needs,
                Account = persistedSource,
                AccountId = persistedSource.Id,
                Tag = childTag,
                TagId = childTag.Id
            };
            var childLog = new ExpenseLog
            {
                Id = 200,
                Expense = childExpense,
                ExpenseId = childExpense.Id,
                Account = persistedSource,
                AccountId = persistedSource.Id,
                ParentLogId = 10,
                Amount = 25m,
                DeductedOn = new DateTime(2026, 5, 31)
            };
            appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ExpenseLog>>([childLog]));
            appData.GetTagByIdAsync(2, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Tag?>(childTag));

            vm.BeginSplitModeAsync().GetAwaiter().GetResult();
            var row = Assert.Single(vm.SplitRows);
            row.NameText = "Updated child";
            row.AmountText = 40m;
            row.SelectedExpenseCategory = ExpenseCategory.Wants;
            row.SelectedTag = new TagVM { Id = 2, Name = "Travel", HexCode = "#3B82F6" };

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            appData.Received(1).UpdateExpense(Arg.Is<Expense>(expense =>
                expense.Id == 30 &&
                expense.Name == "Updated child" &&
                expense.Amount == 40m &&
                expense.ExpenseCategory == ExpenseCategory.Wants &&
                expense.TagId == 2));
            appData.Received(1).UpdateExpenseLog(Arg.Is<ExpenseLog>(log =>
                log.Id == 200 &&
                log.Amount == 40m &&
                log.ParentLogId == 10 &&
                !log.IsForDeletion));
            _ = appData.DidNotReceive().AddExpenseLogAsync(
                Arg.Is<ExpenseLog>(log => log.ParentLogId == 10),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void SaveAsync_FromExistingSplitMode_RemovedChildPersistsEditsAndMarksForDeletion()
    {
        RunInSta(() =>
        {
            var (vm, appData, persistedSource) = CreateVmWithDependencies(amount: 100m);
            var tag = new Tag { Id = 1, Name = "General", HexCode = "#22C55E" };
            var childExpense = new Expense
            {
                Id = 30,
                Name = "Old child",
                Amount = 25m,
                ExpenseCategory = ExpenseCategory.Needs,
                Account = persistedSource,
                AccountId = persistedSource.Id,
                Tag = tag,
                TagId = tag.Id
            };
            var childLog = new ExpenseLog
            {
                Id = 200,
                Expense = childExpense,
                ExpenseId = childExpense.Id,
                Account = persistedSource,
                AccountId = persistedSource.Id,
                ParentLogId = 10,
                Amount = 25m,
                DeductedOn = new DateTime(2026, 5, 31)
            };
            appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ExpenseLog>>([childLog]));

            vm.BeginSplitModeAsync().GetAwaiter().GetResult();
            var row = Assert.Single(vm.SplitRows);
            row.NameText = "Edited then removed";
            row.AmountText = 45m;
            vm.RemoveSplitRow(row);

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            appData.Received(1).UpdateExpense(Arg.Is<Expense>(expense =>
                expense.Id == 30 &&
                expense.Name == "Edited then removed" &&
                expense.Amount == 45m));
            appData.Received(1).UpdateExpenseLog(Arg.Is<ExpenseLog>(log =>
                log.Id == 200 &&
                log.Amount == 45m &&
                log.IsForDeletion));
        });
    }

    [Fact]
    public void SaveAsync_NormalMode_WhenParentAccountChanges_UpdatesChildAccounts()
    {
        RunInSta(() =>
        {
            var (vm, appData, persistedSource) = CreateVmWithDependencies(amount: 100m);
            var targetSource = new Account
            {
                Id = 2,
                Name = "Credit",
                AccountType = AccountType.Credit,
                IsEnabled = true
            };
            var childExpense = new Expense
            {
                Id = 30,
                Name = "Child",
                Amount = 25m,
                Account = persistedSource,
                AccountId = 1
            };
            var childLog = new ExpenseLog
            {
                Id = 200,
                Expense = childExpense,
                ExpenseId = childExpense.Id,
                Account = persistedSource,
                AccountId = 1,
                ParentLogId = 10,
                Amount = 25m,
                DeductedOn = new DateTime(2026, 5, 31)
            };
            appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ExpenseLog>>([childLog]));
            appData.GetAccountByIdAsync(2, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Account?>(targetSource));
            vm.Accounts.Add(new AccountVM
            {
                Id = 2,
                Name = "Credit",
                AccountType = AccountType.Credit,
                IsEnabled = true
            });

            vm.BeginEditingAsync().GetAwaiter().GetResult();
            vm.SelectedAccount = vm.Accounts.Single(account => account.Id == 2);

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            appData.Received(1).UpdateExpense(Arg.Is<Expense>(expense =>
                expense.Id == 30 &&
                expense.AccountId == 2 &&
                ReferenceEquals(expense.Account, targetSource)));
            appData.Received(1).UpdateExpenseLog(Arg.Is<ExpenseLog>(log =>
                log.Id == 200 &&
                log.AccountId == 2 &&
                ReferenceEquals(log.Account, targetSource)));
        });
    }

    [Fact]
    public void SaveAsync_FromSplitMode_RequiresAtLeastOneAmount()
    {
        RunInSta(() =>
        {
            var (vm, appData, _) = CreateVmWithDependencies(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].NameText = "Pending split";
            vm.AmountText = 0m;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Add at least one split amount before saving.", result.ErrorMessage);
            appData.DidNotReceive().RemoveExpenseLog(Arg.Any<ExpenseLog>());
            appData.DidNotReceive().RemoveExpense(Arg.Any<Expense>());
        });
    }

    [Fact]
    public void SaveAsync_FromSplitMode_RejectsNegativeSplitRowAmounts()
    {
        RunInSta(() =>
        {
            var (vm, appData, _) = CreateVmWithDependencies(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].NameText = "Invalid split";
            vm.SplitRows[0].AmountText = -10m;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Split amounts cannot be negative.", result.ErrorMessage);
            appData.DidNotReceive().UpdateExpenseLog(Arg.Any<ExpenseLog>());
            appData.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void SaveAsync_FromSplitMode_RejectsSplitRowsAboveOriginalAmount()
    {
        RunInSta(() =>
        {
            var (vm, appData, _) = CreateVmWithDependencies(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].NameText = "Groceries";
            vm.SplitRows[0].AmountText = 130m;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Split amounts exceed the original expense amount.", result.ErrorMessage);
            appData.DidNotReceive().UpdateExpenseLog(Arg.Any<ExpenseLog>());
            appData.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void SaveAsync_FromSplitMode_KeepsParentFullAmountAndAddsChildExpenses()
    {
        RunInSta(() =>
        {
            var recipient = new MessageCaptureRecipient();
            WeakReferenceMessenger.Default.Register<MessageCaptureRecipient, RecordLogMemoryMessage>(
                recipient,
                static (target, message) => target.RecordLogMemoryMessages.Add(message));

            try
            {
            var (vm, appData, persistedSource) = CreateVmWithDependencies(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].NameText = "Groceries";
            vm.SplitRows[0].AmountText = 30m;
            vm.SplitRows[0].IsIoU = true;
            vm.AddSplitRow();
            vm.SplitRows[1].NameText = "Transport";
            vm.SplitRows[1].AmountText = 20m;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.False(vm.IsEditing);
            Assert.False(vm.IsSplitMode);
            Assert.Empty(vm.SplitRows);

            appData.DidNotReceive().RemoveExpenseLog(Arg.Any<ExpenseLog>());
            appData.DidNotReceive().RemoveExpense(Arg.Any<Expense>());
            appData.DidNotReceive().UpdateExpenseLog(Arg.Is<ExpenseLog>(log => log.Id == 10 && log.IsForDeletion));
            appData.DidNotReceive().UpdateAccount(Arg.Any<Account>());
            Assert.Equal(1_000m, persistedSource.Balance);

            _ = appData.Received(1).AddExpenseLogAsync(
                Arg.Is<ExpenseLog>(log =>
                    log.Amount == 30m &&
                    log.ParentLogId == 10 &&
                    log.AccountId == 1 &&
                    log.IsIoU),
                Arg.Any<CancellationToken>());
            _ = appData.Received(1).AddExpenseLogAsync(
                Arg.Is<ExpenseLog>(log =>
                    log.Amount == 20m &&
                    log.ParentLogId == 10 &&
                    log.AccountId == 1 &&
                    !log.IsIoU),
                Arg.Any<CancellationToken>());
            _ = appData.Received(1).AddExpenseAsync(
                Arg.Is<Expense>(expense => expense.Amount == 30m && expense.IsIoU),
                Arg.Any<CancellationToken>());
            _ = appData.Received(1).AddExpenseAsync(
                Arg.Is<Expense>(expense => expense.Amount == 20m && !expense.IsIoU),
                Arg.Any<CancellationToken>());

            _ = appData.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

            Assert.Single(recipient.RecordLogMemoryMessages);
            var composite = Assert.IsType<CompositeLogMemoryAction>(recipient.RecordLogMemoryMessages[0].Value);
            Assert.Equal("Split expense", composite.Description);
            var addActions = composite.Actions.Select(Assert.IsType<AddExpenseLogMemoryAction>).ToList();
            Assert.Equal(2, addActions.Count);
            Assert.All(addActions, action => Assert.False(action.ShouldAdjustAccountTotals));
            Assert.All(addActions, action => Assert.True(action.Snapshot.ExpenseId > 0));
            Assert.All(addActions, action => Assert.True(action.Snapshot.ExpenseLogId > 0));
            Assert.All(addActions, action => Assert.Equal(1, action.Snapshot.TagId));
            Assert.All(addActions, action => Assert.Equal(10, action.Snapshot.ParentLogId));
            Assert.All(addActions, action => Assert.Equal(new DateTime(2026, 5, 31), action.Snapshot.DeductedOn));

            Assert.Contains(addActions, action =>
                action.Snapshot.Amount == 30m &&
                action.Snapshot.ExpenseName == "Groceries" &&
                action.Snapshot.Notes == string.Empty &&
                !action.Snapshot.IsForDeletion &&
                action.Snapshot.IsIoU);
            Assert.Contains(addActions, action =>
                action.Snapshot.Amount == 20m &&
                action.Snapshot.ExpenseName == "Transport" &&
                action.Snapshot.Notes == string.Empty &&
                !action.Snapshot.IsForDeletion &&
                !action.Snapshot.IsIoU);
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(recipient);
            }
        });
    }

    [Fact]
    public void SaveAsync_FromSplitMode_WhenAccountAlreadyTracked_AddsChildExpenses()
    {
        RunInSta(() => SaveAsyncFromSplitModeWhenAccountAlreadyTrackedAddsChildExpensesAsync()
            .GetAwaiter()
            .GetResult());
    }

    [Fact]
    public void SaveAsync_FromSplitMode_KeepsBudgetReconciliationParent()
    {
        RunInSta(() =>
        {
            var (vm, appData, _) = CreateVmWithDependencies(amount: 100m, isBudgetReconciliation: true);
            vm.BeginSplitMode();
            vm.NameText = "Remainder";
            vm.AddSplitRow();
            vm.SplitRows[0].NameText = "Groceries";
            vm.SplitRows[0].AmountText = 30m;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            appData.DidNotReceive().RemoveExpenseLog(Arg.Any<ExpenseLog>());
            appData.DidNotReceive().RemoveExpense(Arg.Any<Expense>());
            appData.DidNotReceive().UpdateExpenseLog(Arg.Is<ExpenseLog>(log => log.Id == 10 && log.IsForDeletion));
            _ = appData.Received(1).AddExpenseLogAsync(
                Arg.Is<ExpenseLog>(log => log.Amount == 30m && log.ParentLogId == 10),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void SaveAsync_FromSplitMode_WithLegacyRemainderFlag_KeepsParentFullAmountAndAddsSplitExpenses()
    {
        RunInSta(() =>
        {
            var recipient = new MessageCaptureRecipient();
            WeakReferenceMessenger.Default.Register<MessageCaptureRecipient, RecordLogMemoryMessage>(
                recipient,
                static (target, message) => target.RecordLogMemoryMessages.Add(message));

            try
            {
                var (vm, appData, _) = CreateVmWithDependencies(amount: 100m);
                vm.BeginSplitMode();
                vm.NameText = "Remainder";
                vm.NoteText = "Remainder note";
                vm.SelectedExpenseCategory = ExpenseCategory.Wants;
                vm.AddSplitRow();
                vm.SplitRows[0].NameText = "Groceries";
                vm.SplitRows[0].AmountText = 30m;
                vm.AddSplitRow();
                vm.SplitRows[1].NameText = "Transport";
                vm.SplitRows[1].AmountText = 20m;

                var result = vm.SaveAsync(keepParentExpenseWhenRemainder: true).GetAwaiter().GetResult();

                Assert.True(result.IsSuccess);
                Assert.False(vm.IsEditing);
                Assert.False(vm.IsSplitMode);
                Assert.Empty(vm.SplitRows);

                appData.DidNotReceive().RemoveExpenseLog(Arg.Any<ExpenseLog>());
                appData.DidNotReceive().RemoveExpense(Arg.Any<Expense>());
                appData.DidNotReceive().UpdateAccount(Arg.Any<Account>());

                _ = appData.Received(1).AddExpenseAsync(
                    Arg.Is<Expense>(expense =>
                        expense.Amount == 30m &&
                        expense.ExpenseCategory == ExpenseCategory.Wants &&
                        expense.TagId == 1),
                    Arg.Any<CancellationToken>());
                _ = appData.Received(1).AddExpenseAsync(
                    Arg.Is<Expense>(expense =>
                        expense.Amount == 20m &&
                        expense.ExpenseCategory == ExpenseCategory.Wants &&
                        expense.TagId == 1),
                    Arg.Any<CancellationToken>());
                _ = appData.DidNotReceive().AddExpenseAsync(
                    Arg.Is<Expense>(expense => expense.Amount == 50m),
                    Arg.Any<CancellationToken>());

                Assert.Single(recipient.RecordLogMemoryMessages);
                var composite = Assert.IsType<CompositeLogMemoryAction>(recipient.RecordLogMemoryMessages[0].Value);
                Assert.Equal("Split expense", composite.Description);
                var addActions = composite.Actions.Select(Assert.IsType<AddExpenseLogMemoryAction>).ToList();
                Assert.Equal(2, addActions.Count);
                Assert.All(addActions, action => Assert.False(action.ShouldAdjustAccountTotals));
                Assert.All(addActions, action => Assert.Equal(10, action.Snapshot.ParentLogId));
                Assert.Contains(addActions, action =>
                    action.Snapshot.Amount == 30m &&
                    action.Snapshot.ExpenseName == "Groceries");
                Assert.Contains(addActions, action =>
                    action.Snapshot.Amount == 20m &&
                    action.Snapshot.ExpenseName == "Transport");
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(recipient);
            }
        });
    }

    [Fact]
    public void HasValidChangesToPersistOnClose_InSplitMode_RequiresRowsWithAmounts()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();

            Assert.True(vm.HasSplitRowsWithoutAmounts);
            Assert.False(vm.HasValidChangesToPersistOnClose());

            vm.SplitRows[0].AmountText = 25m;

            Assert.True(vm.HasSplitRowsWithAmounts);
            Assert.True(vm.HasValidChangesToPersistOnClose());
        });
    }

    private static ExpenseDetailVM CreateVm(decimal amount = 100m)
    {
        return CreateVmWithDependencies(amount).Vm;
    }

    private static (ExpenseDetailVM Vm, IAppDataService AppData, Account PersistedSource) CreateVmWithDependencies(
        decimal amount = 100m,
        bool isBudgetReconciliation = false)
    {
        var source = new AccountVM
        {
            Id = 1,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 1_000m,
            IsEnabled = true
        };

        var tag = new TagVM
        {
            Id = 1,
            Name = "General",
            HexCode = "#22C55E",
            IsSystemTag = false
        };

        var main = CreateMainViewModel(source, tag);
        var appData = Substitute.For<IAppDataService>();
        appData.GetTagsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Tag>>(
            [
                new Tag { Id = 1, Name = "General", HexCode = "#22C55E", IsSystemTag = false },
                new Tag { Id = 2, Name = "Travel", HexCode = "#3B82F6", IsSystemTag = false }
            ]));
        appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLog>>([]));

        var persistedSource = new Account
        {
            Id = 1,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 1_000m,
            IsEnabled = true
        };

        var persistedTag = new Tag
        {
            Id = 1,
            Name = isBudgetReconciliation ? SystemTags.BudgetReconciliationName : "General",
            HexCode = isBudgetReconciliation ? SystemTags.BudgetReconciliationHexCode : "#22C55E",
            IsSystemTag = isBudgetReconciliation
        };

        var persistedExpense = new Expense
        {
            Id = 20,
            Name = "Parent expense",
            Amount = amount,
            ExpenseCategory = ExpenseCategory.Needs,
            Account = persistedSource,
            AccountId = persistedSource.Id,
            Tag = persistedTag,
            TagId = persistedTag.Id
        };

        var persistedLog = new ExpenseLog
        {
            Id = 10,
            ExpenseId = persistedExpense.Id,
            AccountId = persistedSource.Id,
            Expense = persistedExpense,
            Account = persistedSource,
            Amount = amount,
            DeductedOn = new DateTime(2026, 5, 31),
            Notes = "Original note"
        };

        appData.GetExpenseLogByLogIdAsync(10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ExpenseLog?>(persistedLog));
        appData.GetAccountByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Account?>(persistedSource));
        appData.GetTagByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Tag?>(persistedTag));
        appData.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var nextExpenseId = 100;
        appData.When(service => service.AddExpenseAsync(Arg.Any<Expense>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var expense = call.Arg<Expense>();
                if (expense.Id <= 0)
                    expense.Id = nextExpenseId++;
            });

        var nextExpenseLogId = 200;
        appData.When(service => service.AddExpenseLogAsync(Arg.Any<ExpenseLog>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var expenseLog = call.Arg<ExpenseLog>();
                if (expenseLog.Id <= 0)
                    expenseLog.Id = nextExpenseLogId++;
            });

        return (new ExpenseDetailVM(main, new ExpenseLogVM
        {
            Id = 10,
            Amount = amount,
            DeductedOn = new DateTime(2026, 5, 31),
            Notes = "Original note",
            Account = source,
            Expense = new ExpenseVM
            {
                Id = 20,
                Name = "Parent expense",
                Amount = amount,
                ExpenseCategory = ExpenseCategory.Needs,
                Account = source,
                Tag = tag
            }
        }, appData), appData, persistedSource);
    }

    private static async Task SaveAsyncFromSplitModeWhenAccountAlreadyTrackedAddsChildExpensesAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<FluxoDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new FluxoDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var account = new Account
        {
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 1_000m,
            IsEnabled = true,
            PinnedOnUI = true
        };
        var tag = new Tag
        {
            Name = "General",
            HexCode = "#22C55E",
            IsSystemTag = false
        };
        var parentExpense = new Expense
        {
            Name = "Parent expense",
            Amount = 100m,
            ExpenseCategory = ExpenseCategory.Needs,
            Account = account,
            Tag = tag
        };
        var parentLog = new ExpenseLog
        {
            Expense = parentExpense,
            Account = account,
            Amount = 100m,
            DeductedOn = new DateTime(2026, 5, 31),
            Notes = "Original note",
            IsForDeletion = false
        };

        await dbContext.ExpenseLogs.AddAsync(parentLog);
        await dbContext.SaveChangesAsync();

        var accountId = account.Id;
        var tagId = tag.Id;
        var parentExpenseId = parentExpense.Id;
        var parentLogId = parentLog.Id;

        dbContext.Entry(parentLog).State = EntityState.Detached;
        dbContext.Entry(parentExpense).State = EntityState.Detached;
        dbContext.Entry(tag).State = EntityState.Detached;
        Assert.Single(dbContext.ChangeTracker.Entries<Account>());

        using var unitOfWork = CreateUnitOfWork(dbContext);
        var appData = new AppDataService(unitOfWork);
        var sourceVm = new AccountVM
        {
            Id = accountId,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 1_000m,
            IsEnabled = true
        };
        var tagVm = new TagVM
        {
            Id = tagId,
            Name = "General",
            HexCode = "#22C55E",
            IsSystemTag = false
        };
        var main = CreateMainViewModel(sourceVm, tagVm);
        var vm = new ExpenseDetailVM(main, new ExpenseLogVM
        {
            Id = parentLogId,
            Amount = 100m,
            DeductedOn = new DateTime(2026, 5, 31),
            Notes = "Original note",
            Account = sourceVm,
            Expense = new ExpenseVM
            {
                Id = parentExpenseId,
                Name = "Parent expense",
                Amount = 100m,
                ExpenseCategory = ExpenseCategory.Needs,
                Account = sourceVm,
                Tag = tagVm
            }
        }, appData);

        vm.BeginSplitMode();
        vm.AddSplitRow();
        vm.SplitRows[0].NameText = "Groceries";
        vm.SplitRows[0].AmountText = 30m;
        vm.AddSplitRow();
        vm.SplitRows[1].NameText = "Transport";
        vm.SplitRows[1].AmountText = 20m;

        var result = await vm.SaveAsync();

        Assert.True(result.IsSuccess, result.ErrorMessage);

        var parent = await dbContext.ExpenseLogs
            .AsNoTracking()
            .SingleAsync(log => log.Id == parentLogId);
        Assert.Equal(100m, parent.Amount);
        Assert.Null(parent.ParentLogId);
        Assert.False(parent.IsForDeletion);

        var children = await dbContext.ExpenseLogs
            .AsNoTracking()
            .Include(log => log.Expense)
            .Where(log => log.ParentLogId == parentLogId)
            .OrderBy(log => log.Amount)
            .ToListAsync();

        Assert.Equal(2, children.Count);
        Assert.Contains(children, log =>
            log.Amount == 20m &&
            log.AccountId == accountId &&
            log.Expense.AccountId == accountId &&
            log.Expense.TagId == tagId &&
            log.Expense.Name == "Transport");
        Assert.Contains(children, log =>
            log.Amount == 30m &&
            log.AccountId == accountId &&
            log.Expense.AccountId == accountId &&
            log.Expense.TagId == tagId &&
            log.Expense.Name == "Groceries");
    }

    private static MainVM CreateMainViewModel(AccountVM source, TagVM tag)
    {
        var messenger = new WeakReferenceMessenger();
        var mapper = Substitute.For<IMapper>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userSettings = Substitute.For<IUserSettingsRepository>();
        userSettings.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>([]));
        unitOfWork.UserSettings.Returns(userSettings);

        var incomeLogs = Substitute.For<IIncomeLogRepository>();
        incomeLogs.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IncomeLog>>([]));
        unitOfWork.IncomeLogs.Returns(incomeLogs);

        var runner = new InlineDataOperationRunner(unitOfWork);

        var dashboard = new DashboardVM(
            new NotificationPanelVM(
                Substitute.For<IExpenseService>(),
                Substitute.For<IExpenseLogService>(),
                Substitute.For<IAccountService>(),
                runner,
                mapper,
                messenger: messenger),
            new BudgetAllocationPanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<IAccountService>(),
                Substitute.For<ITagService>(),
                runner,
                mapper,
                messenger),
            new SpentAllowancePanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<IAccountService>(),
                runner,
                mapper,
                messenger),
            new SavingGoalsPanelVM(runner, mapper, messenger),
            new UpcomingEventsPanelVM(runner, mapper, messenger: messenger),
            new MainViewModeToggleVM(messenger));
        var main = new MainVM(
            runner,
            dashboard,
            new DaySpinnerVM(messenger),
            null);

        main.BudgetPanel.Accounts.Add(source);
        main.BudgetPanel.Tags = new ObservableCollection<TagVM>([tag]);
        main.BudgetPanel.OtherTags = new ObservableCollection<TagVM>(
        [
            new TagVM { Id = 2, Name = "Travel", HexCode = "#3B82F6", IsSystemTag = false }
        ]);

        return main;
    }

    private static UnitOfWork CreateUnitOfWork(FluxoDbContext dbContext)
    {
        return new UnitOfWork(
            dbContext,
            new ExpenseRepository(dbContext),
            new ExpenseLogRepository(dbContext),
            new IncomeLogRepository(dbContext),
            new TagRepository(dbContext),
            new SavingGoalRepository(dbContext),
            new AccountRepository(dbContext),
            new RecurringTransactionRepository(dbContext),
            new NotificationRepository(dbContext),
            new UserSettingsRepository(dbContext),
            new BudgetAllocationRepository(dbContext));
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

    private sealed class MessageCaptureRecipient
    {
        public List<RecordLogMemoryMessage> RecordLogMemoryMessages { get; } = [];
    }
}
