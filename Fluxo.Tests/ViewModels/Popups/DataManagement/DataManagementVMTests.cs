using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Popups.DataManagement;
using NSubstitute;
using Xunit;
using System.ComponentModel;

namespace Fluxo.Tests.ViewModels.Popups.DataManagement;

public sealed class DataManagementVMTests
{
    [Fact]
    public void SpendingSourcesUnchecked_DisablesExpensesIncomesAndRecurringTransactions()
    {
        var vm = new DataManagementVM(Substitute.For<IUserBackupService>());

        vm.SetEntityChecked(DataManagementEntityKind.SpendingSources, false);

        Assert.False(vm.GetEntity(DataManagementEntityKind.Expenses).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Expenses).IsChecked);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Incomes).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Incomes).IsChecked);
        Assert.False(vm.GetEntity(DataManagementEntityKind.RecurringTransactions).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.RecurringTransactions).IsChecked);
    }

    [Fact]
    public void SpendingSourcesUncheckedFromEntityToggle_DisablesExpensesIncomesAndRecurringTransactions()
    {
        var vm = new DataManagementVM(Substitute.For<IUserBackupService>());

        vm.GetEntity(DataManagementEntityKind.SpendingSources).IsChecked = false;

        Assert.False(vm.GetEntity(DataManagementEntityKind.Expenses).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Expenses).IsChecked);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Incomes).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Incomes).IsChecked);
        Assert.False(vm.GetEntity(DataManagementEntityKind.RecurringTransactions).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.RecurringTransactions).IsChecked);
    }

    [Fact]
    public void ApplyManifest_DisablesMissingEntityGroups()
    {
        var vm = new DataManagementVM(Substitute.For<IUserBackupService>());
        var manifest = new UserBackupManifest(
            1,
            DateTime.UtcNow,
            new HashSet<DataManagementEntityKind> { DataManagementEntityKind.Tags });

        vm.ApplyManifest(manifest);

        Assert.True(vm.GetEntity(DataManagementEntityKind.Tags).IsEnabled);
        Assert.True(vm.GetEntity(DataManagementEntityKind.Tags).IsChecked);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Goals).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Goals).IsChecked);
    }

    [Fact]
    public void ApplyManifest_WithSpendingSourcesOnly_KeepsDependentsDisabledAndUnchecked()
    {
        var vm = new DataManagementVM(Substitute.For<IUserBackupService>());
        var manifest = new UserBackupManifest(
            1,
            DateTime.UtcNow,
            new HashSet<DataManagementEntityKind> { DataManagementEntityKind.SpendingSources });

        vm.ApplyManifest(manifest);

        Assert.True(vm.GetEntity(DataManagementEntityKind.SpendingSources).IsEnabled);
        Assert.True(vm.GetEntity(DataManagementEntityKind.SpendingSources).IsChecked);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Expenses).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Expenses).IsChecked);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Incomes).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.Incomes).IsChecked);
        Assert.False(vm.GetEntity(DataManagementEntityKind.RecurringTransactions).IsEnabled);
        Assert.False(vm.GetEntity(DataManagementEntityKind.RecurringTransactions).IsChecked);
    }

    [Fact]
    public void DecisionSetDirectly_RaisesSelectionPropertyNotifications()
    {
        var item = new DataManagementConflictItemVM(
            new UserBackupConflict("conflict", DataManagementEntityKind.Expenses, "Sample"));
        var changedProperties = new List<string>();

        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
                changedProperties.Add(args.PropertyName);
        };

        item.Decision = DataManagementConflictDecision.Append;

        Assert.Contains(nameof(INotifyPropertyChanged.PropertyChanged).Replace("PropertyChanged", "Decision"), changedProperties);
        Assert.Contains(nameof(DataManagementConflictItemVM.IsReplaceSelected), changedProperties);
        Assert.Contains(nameof(DataManagementConflictItemVM.IsAppendSelected), changedProperties);
        Assert.Contains(nameof(DataManagementConflictItemVM.IsIgnoreSelected), changedProperties);
    }
}
