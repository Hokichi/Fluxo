using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Popups;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class SpendingSourceDetailVMTests
{
    [Fact]
    public async Task LoadAsync_WhenNoHistory_SetsEmptyHistoryState()
    {
        var source = CreateSource(1, "Checking", SpendingSourceType.Checking);
        var appData = CreateAppData([source], [], []);
        var sut = new SpendingSourceDetailVM(null!, source.Id, appData);

        await sut.LoadAsync();

        Assert.False(sut.HasRecentActivities);
    }

    [Fact]
    public async Task LoadAsync_WhenHistoryExists_SetsNonEmptyHistoryState()
    {
        var source = CreateSource(1, "Checking", SpendingSourceType.Checking);
        var appData = CreateAppData(
            [source],
            [new ExpenseLog { Id = 10, SpendingSourceId = source.Id, Amount = 20m, DeductedOn = DateTime.Today }],
            []);
        var sut = new SpendingSourceDetailVM(null!, source.Id, appData);

        await sut.LoadAsync();

        Assert.True(sut.HasRecentActivities);
    }

    [Fact]
    public async Task LoadAsync_WhenNoTransferTarget_DisablesTransfer()
    {
        var source = CreateSource(1, "Checking", SpendingSourceType.Checking);
        var appData = CreateAppData([source], [], []);
        var sut = new SpendingSourceDetailVM(null!, source.Id, appData);

        await sut.LoadAsync();

        Assert.False(sut.CanTransfer);
    }

    [Fact]
    public async Task LoadAsync_WhenTransferTargetExists_AllowsTransfer()
    {
        var source = CreateSource(1, "Checking", SpendingSourceType.Checking);
        var target = CreateSource(2, "Savings", SpendingSourceType.Saving);
        var appData = CreateAppData([source, target], [], []);
        var sut = new SpendingSourceDetailVM(null!, source.Id, appData);

        await sut.LoadAsync();

        Assert.True(sut.CanTransfer);
    }

    [Fact]
    public async Task ShouldConfirmDisablingOnlyEnabledSourceAsync_WhenOnlyEnabledSource_ReturnsTrue()
    {
        var source = CreateSource(1, "Checking", SpendingSourceType.Checking);
        var disabled = CreateSource(2, "Cash", SpendingSourceType.Cash, isEnabled: false);
        var appData = CreateAppData([source, disabled], [], []);
        var sut = new SpendingSourceDetailVM(null!, source.Id, appData);

        await sut.LoadAsync();

        Assert.True(await sut.ShouldConfirmDisablingOnlyEnabledSourceAsync());
    }

    [Fact]
    public async Task ShouldConfirmDisablingOnlyEnabledSourceAsync_WhenAnotherEnabledSourceExists_ReturnsFalse()
    {
        var source = CreateSource(1, "Checking", SpendingSourceType.Checking);
        var target = CreateSource(2, "Cash", SpendingSourceType.Cash);
        var appData = CreateAppData([source, target], [], []);
        var sut = new SpendingSourceDetailVM(null!, source.Id, appData);

        await sut.LoadAsync();

        Assert.False(await sut.ShouldConfirmDisablingOnlyEnabledSourceAsync());
    }

    private static IAppDataService CreateAppData(
        IReadOnlyList<SpendingSource> sources,
        IReadOnlyList<ExpenseLog> expenseLogs,
        IReadOnlyList<IncomeLog> incomeLogs)
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetSpendingSourceByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<SpendingSource?>(
                sources.FirstOrDefault(source => source.Id == call.ArgAt<int>(0))));
        appData.GetSpendingSourcesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(sources));
        appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expenseLogs));
        appData.GetIncomeLogsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(incomeLogs));
        return appData;
    }

    private static SpendingSource CreateSource(
        int id,
        string name,
        SpendingSourceType type,
        bool isEnabled = true)
    {
        return new SpendingSource
        {
            Id = id,
            Name = name,
            SpendingSourceType = type,
            Balance = 100m,
            IsEnabled = isEnabled,
            ShowOnUI = true
        };
    }
}
