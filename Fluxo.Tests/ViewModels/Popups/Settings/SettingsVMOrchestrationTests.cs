using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Settings;

public sealed class SettingsVMOrchestrationTests
{
    [Fact]
    public void MessageContracts_AreAccessible()
    {
        var operation = new SettingsOperationCorrelation(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var loadRequested = new SettingsLoadRequestedMessage(operation);
        var tabLoaded = new SettingsTabLoadedMessage(new SettingsTabLoaded(
            operation,
            SettingsTabKey.Budget,
            IsSuccess: true,
            ErrorMessage: null));
        var pendingChanged = new SettingsPendingChangesChangedMessage(new SettingsPendingChangesChanged(
            SettingsTabKey.Tags,
            HasPendingChanges: true));
        var applyRequested = new SettingsApplyRequestedMessage(operation);
        var contribution = new SettingsApplyContributionMessage(new SettingsApplyContribution(
            operation,
            SettingsTabKey.Personalization,
            IsSuccess: true,
            ErrorMessage: null,
            SettingChanges:
            [
                new SettingsSettingChange("PreferredAppName", "Fluxo", "Fluxo Pro")
            ],
            MemoryActions:
            [
                new TestLogMemoryAction("Rename app")
            ],
            UsernameChange: new SettingsUsernameChange("Fluxo", "Fluxo Pro")));
        var revertRequested = new SettingsRevertRequestedMessage(operation);
        var dataChanged = new SettingsDataChangedMessage(SettingsDataChangedScope.SpendingSources | SettingsDataChangedScope.Tags);

        Assert.Equal(operation, loadRequested.Value);
        Assert.Equal(operation, tabLoaded.Value.Operation);
        Assert.Equal(SettingsTabKey.Budget, tabLoaded.Value.TabKey);
        Assert.True(tabLoaded.Value.IsSuccess);
        Assert.Equal(SettingsTabKey.Tags, pendingChanged.Value.TabKey);
        Assert.True(pendingChanged.Value.HasPendingChanges);
        Assert.Equal(operation, applyRequested.Value);
        Assert.Equal(SettingsTabKey.Personalization, contribution.Value.TabKey);
        Assert.Single(contribution.Value.SettingChanges);
        Assert.Single(contribution.Value.MemoryActions);
        Assert.Equal("Fluxo Pro", contribution.Value.UsernameChange?.CurrentValue);
        Assert.Equal(operation, revertRequested.Value);
        Assert.True(dataChanged.Value.HasFlag(SettingsDataChangedScope.SpendingSources));
        Assert.True(dataChanged.Value.HasFlag(SettingsDataChangedScope.Tags));
    }

    private sealed class TestLogMemoryAction(string description) : ILogMemoryAction
    {
        public string Description { get; } = description;

        public Task UndoAsync(Fluxo.Core.Interfaces.IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RedoAsync(Fluxo.Core.Interfaces.IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
