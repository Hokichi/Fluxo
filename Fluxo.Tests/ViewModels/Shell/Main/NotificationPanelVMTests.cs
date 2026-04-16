using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class NotificationPanelVMTests
{
    [Fact]
    public void LoadSnapshot_WhenCalledTwice_DoesNotDuplicateSystemNotifications()
    {
        var vm = new NotificationPanelVM();
        var dueDate = DateTime.Today.AddDays(7);
        var spendingSources = new List<SpendingSourceVM>
        {
            new()
            {
                Id = 1,
                Name = "Visa",
                SpendingSourceType = SpendingSourceType.Credit,
                DueDate = dueDate,
                AccountLimit = 1000m,
                SpentAmount = 250m
            }
        };

        var snapshot = new NotificationPanelSnapshot(
            Expenses: [],
            ExpenseLogs: [],
            SpendingSources: spendingSources);

        vm.LoadSnapshot(snapshot);
        vm.LoadSnapshot(snapshot);

        Assert.Equal(1, vm.NotificationCount);
        Assert.Equal(1, vm.Notifications.Select(notification => notification.Key).Distinct().Count());
    }
}