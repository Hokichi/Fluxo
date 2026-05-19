namespace Fluxo.ViewModels.Popups;

public readonly record struct NotificationChecklistActionDecision(
    int EntityId,
    NotificationChecklistItemActionType Action,
    int? SelectedSourceId,
    decimal? Amount = null,
    int? SelectedTagId = null,
    int? SelectedGoalId = null,
    bool UpdateRecurringAmount = false);
