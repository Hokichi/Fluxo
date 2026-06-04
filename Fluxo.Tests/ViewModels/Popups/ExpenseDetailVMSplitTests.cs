using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;
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
            row.SelectedTag = new ExpenseTagVM { Id = 2, Name = "Travel", HexCode = "#3B82F6", IsSystemTag = false };

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
    public void CancelEditing_FromSplitMode_RestoresOriginalAmountAndLeavesSplitMode()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
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
            vm.NameText = "Updated expense";

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.False(vm.IsSplitMode);
            Assert.Empty(vm.SplitRows);
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
    public void SaveAsync_FromSplitMode_RequiresSplitTotalToMatchOriginalAmount()
    {
        RunInSta(() =>
        {
            var (vm, appData, _) = CreateVmWithDependencies(amount: 100m);
            vm.BeginSplitMode();
            vm.AddSplitRow();
            vm.SplitRows[0].NameText = "Groceries";
            vm.SplitRows[0].AmountText = 30m;
            vm.AmountText = 60m;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.False(result.IsSuccess);
            Assert.Equal("Split total must match the original expense amount.", result.ErrorMessage);
            appData.DidNotReceive().UpdateExpenseLog(Arg.Any<ExpenseLog>());
            appData.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void SaveAsync_FromSplitMode_DeletesOriginalAndAddsReplacementExpenses()
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
            vm.AddSplitRow();
            vm.SplitRows[0].NameText = "Groceries";
            vm.SplitRows[0].AmountText = 30m;
            vm.AddSplitRow();
            vm.SplitRows[1].NameText = "Transport";
            vm.SplitRows[1].AmountText = 20m;

            var result = vm.SaveAsync().GetAwaiter().GetResult();

            Assert.True(result.IsSuccess);
            Assert.False(vm.IsEditing);
            Assert.False(vm.IsSplitMode);
            Assert.Empty(vm.SplitRows);

            appData.Received(1).UpdateExpenseLog(Arg.Is<ExpenseLog>(log => log.Id == 10 && log.IsForDeletion));
            appData.DidNotReceive().RemoveExpenseLog(Arg.Any<ExpenseLog>());
            appData.DidNotReceive().RemoveExpense(Arg.Any<Expense>());

            appData.Received(1).UpdateSpendingSource(Arg.Is<SpendingSource>(source =>
                source.Id == 1 &&
                source.Balance == 1000m));

            _ = appData.Received(1).AddExpenseAsync(
                Arg.Is<Expense>(expense =>
                    expense.Amount == 50m &&
                    expense.ExpenseCategory == ExpenseCategory.Needs &&
                    expense.ExpenseTagId == 1),
                Arg.Any<CancellationToken>());
            _ = appData.Received(1).AddExpenseAsync(
                Arg.Is<Expense>(expense =>
                    expense.Amount == 30m &&
                    expense.ExpenseCategory == ExpenseCategory.Needs &&
                    expense.ExpenseTagId == 1),
                Arg.Any<CancellationToken>());
            _ = appData.Received(1).AddExpenseAsync(
                Arg.Is<Expense>(expense =>
                    expense.Amount == 20m &&
                    expense.ExpenseCategory == ExpenseCategory.Needs &&
                    expense.ExpenseTagId == 1),
                Arg.Any<CancellationToken>());

            _ = appData.Received(1).AddExpenseLogAsync(
                Arg.Is<ExpenseLog>(log =>
                    log.Amount == 50m &&
                    log.Notes == "Remainder note" &&
                    log.SpendingSourceId == 1),
                Arg.Any<CancellationToken>());
            _ = appData.Received(1).AddExpenseLogAsync(
                Arg.Is<ExpenseLog>(log =>
                    log.Amount == 30m &&
                    log.Notes == string.Empty &&
                    log.SpendingSourceId == 1),
                Arg.Any<CancellationToken>());
            _ = appData.Received(1).AddExpenseLogAsync(
                Arg.Is<ExpenseLog>(log =>
                    log.Amount == 20m &&
                    log.Notes == string.Empty &&
                    log.SpendingSourceId == 1),
                Arg.Any<CancellationToken>());

            _ = appData.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

            Assert.Single(recipient.RecordLogMemoryMessages);
            var composite = Assert.IsType<CompositeLogMemoryAction>(recipient.RecordLogMemoryMessages[0].Value);
            Assert.Equal("Split expense", composite.Description);
            Assert.Equal(4, composite.Actions.Count);

            var deleteAction = Assert.IsType<DeleteExpenseLogMemoryAction>(composite.Actions[0]);
            Assert.NotNull(deleteAction.Snapshot);
            Assert.Equal(10, deleteAction.Snapshot!.ExpenseLogId);

            var addActions = composite.Actions.Skip(1).Select(Assert.IsType<AddExpenseLogMemoryAction>).ToList();
            Assert.Equal(3, addActions.Count);
            Assert.All(addActions, action => Assert.False(action.ShouldAdjustSpendingSourceTotals));
            Assert.All(addActions, action => Assert.True(action.Snapshot.ExpenseId > 0));
            Assert.All(addActions, action => Assert.True(action.Snapshot.ExpenseLogId > 0));
            Assert.All(addActions, action => Assert.Equal(1, action.Snapshot.TagId));
            Assert.All(addActions, action => Assert.Equal(new DateTime(2026, 5, 31), action.Snapshot.DeductedOn));

            Assert.Contains(addActions, action =>
                action.Snapshot.Amount == 50m &&
                action.Snapshot.ExpenseName == "Remainder" &&
                action.Snapshot.Notes == "Remainder note" &&
                !action.Snapshot.IsForDeletion);
            Assert.Contains(addActions, action =>
                action.Snapshot.Amount == 30m &&
                action.Snapshot.ExpenseName == "Groceries" &&
                action.Snapshot.Notes == string.Empty &&
                !action.Snapshot.IsForDeletion);
            Assert.Contains(addActions, action =>
                action.Snapshot.Amount == 20m &&
                action.Snapshot.ExpenseName == "Transport" &&
                action.Snapshot.Notes == string.Empty &&
                !action.Snapshot.IsForDeletion);
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(recipient);
            }
        });
    }

    [Fact]
    public void SaveAsync_FromSplitMode_WithParentRemainder_UpdatesParentAndAddsSplitExpenses()
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

                appData.Received(1).UpdateExpenseLog(Arg.Is<ExpenseLog>(log =>
                    log.Id == 10 &&
                    !log.IsForDeletion &&
                    log.Amount == 50m &&
                    log.Notes == "Remainder note"));
                appData.DidNotReceive().RemoveExpenseLog(Arg.Any<ExpenseLog>());
                appData.DidNotReceive().RemoveExpense(Arg.Any<Expense>());

                appData.Received(1).UpdateSpendingSource(Arg.Is<SpendingSource>(source =>
                    source.Id == 1 &&
                    source.Balance == 1000m));

                _ = appData.Received(1).AddExpenseAsync(
                    Arg.Is<Expense>(expense =>
                        expense.Amount == 30m &&
                        expense.ExpenseCategory == ExpenseCategory.Wants &&
                        expense.ExpenseTagId == 1),
                    Arg.Any<CancellationToken>());
                _ = appData.Received(1).AddExpenseAsync(
                    Arg.Is<Expense>(expense =>
                        expense.Amount == 20m &&
                        expense.ExpenseCategory == ExpenseCategory.Wants &&
                        expense.ExpenseTagId == 1),
                    Arg.Any<CancellationToken>());
                _ = appData.DidNotReceive().AddExpenseAsync(
                    Arg.Is<Expense>(expense => expense.Amount == 50m),
                    Arg.Any<CancellationToken>());

                Assert.Single(recipient.RecordLogMemoryMessages);
                var composite = Assert.IsType<CompositeLogMemoryAction>(recipient.RecordLogMemoryMessages[0].Value);
                Assert.Equal("Split expense", composite.Description);
                Assert.Equal(3, composite.Actions.Count);

                var editAction = Assert.IsType<EditExpenseLogMemoryAction>(composite.Actions[0]);
                Assert.Equal(10, editAction.Before.ExpenseLogId);
                Assert.Equal(50m, editAction.After.Amount);
                Assert.Equal(ExpenseCategory.Wants, editAction.After.ExpenseCategory);
                Assert.Equal("Remainder", editAction.After.ExpenseName);
                Assert.Equal("Remainder note", editAction.After.Notes);

                var addActions = composite.Actions.Skip(1).Select(Assert.IsType<AddExpenseLogMemoryAction>).ToList();
                Assert.Equal(2, addActions.Count);
                Assert.All(addActions, action => Assert.False(action.ShouldAdjustSpendingSourceTotals));
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

    private static (ExpenseDetailVM Vm, IAppDataService AppData, SpendingSource PersistedSource) CreateVmWithDependencies(decimal amount = 100m)
    {
        var source = new SpendingSourceVM
        {
            Id = 1,
            Name = "Checking",
            SpendingSourceType = SpendingSourceType.Checking,
            Balance = 1_000m,
            IsEnabled = true
        };

        var tag = new ExpenseTagVM
        {
            Id = 1,
            Name = "General",
            HexCode = "#22C55E",
            IsSystemTag = false
        };

        var main = CreateMainViewModel(source, tag);
        var appData = Substitute.For<IAppDataService>();
        appData.GetExpenseTagsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseTag>>(
            [
                new ExpenseTag { Id = 1, Name = "General", HexCode = "#22C55E", IsSystemTag = false },
                new ExpenseTag { Id = 2, Name = "Travel", HexCode = "#3B82F6", IsSystemTag = false }
            ]));

        var persistedSource = new SpendingSource
        {
            Id = 1,
            Name = "Checking",
            SpendingSourceType = SpendingSourceType.Checking,
            Balance = 1_000m,
            IsEnabled = true
        };

        var persistedTag = new ExpenseTag
        {
            Id = 1,
            Name = "General",
            HexCode = "#22C55E",
            IsSystemTag = false
        };

        var persistedExpense = new Expense
        {
            Id = 20,
            Name = "Parent expense",
            Amount = amount,
            ExpenseCategory = ExpenseCategory.Needs,
            SpendingSource = persistedSource,
            SpendingSourceId = persistedSource.Id,
            ExpenseTag = persistedTag,
            ExpenseTagId = persistedTag.Id
        };

        var persistedLog = new ExpenseLog
        {
            Id = 10,
            ExpenseId = persistedExpense.Id,
            SpendingSourceId = persistedSource.Id,
            Expense = persistedExpense,
            SpendingSource = persistedSource,
            Amount = amount,
            DeductedOn = new DateTime(2026, 5, 31),
            Notes = "Original note"
        };

        appData.GetExpenseLogByIdAsync(10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ExpenseLog?>(persistedLog));
        appData.GetSpendingSourceByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SpendingSource?>(persistedSource));
        appData.GetExpenseTagByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ExpenseTag?>(persistedTag));
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
            SpendingSource = source,
            Expense = new ExpenseVM
            {
                Id = 20,
                Name = "Parent expense",
                Amount = amount,
                ExpenseCategory = ExpenseCategory.Needs,
                SpendingSource = source,
                ExpenseTag = tag
            }
        }, appData), appData, persistedSource);
    }

    private static MainVM CreateMainViewModel(SpendingSourceVM source, ExpenseTagVM tag)
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
                Substitute.For<ISpendingSourceService>(),
                runner,
                mapper,
                messenger: messenger),
            new BudgetAllocationPanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<ISpendingSourceService>(),
                Substitute.For<ITagService>(),
                runner,
                mapper,
                messenger),
            new SpentAllowancePanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<ISpendingSourceService>(),
                runner,
                mapper,
                messenger),
            new SavingGoalsPanelVM(runner, mapper, messenger),
            new MainViewModeToggleVM(messenger));
        var main = new MainVM(
            runner,
            dashboard,
            new DaySpinnerVM(messenger),
            null);

        main.BudgetPanel.SpendingSources.Add(source);
        main.BudgetPanel.Tags = new ObservableCollection<ExpenseTagVM>([tag]);
        main.BudgetPanel.OtherTags = new ObservableCollection<ExpenseTagVM>(
        [
            new ExpenseTagVM { Id = 2, Name = "Travel", HexCode = "#3B82F6", IsSystemTag = false }
        ]);

        return main;
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
