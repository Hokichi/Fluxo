namespace Fluxo.ViewModels.Popups;

public readonly record struct NotificationChecklistActionDecision(
    int EntityId,
    NotificationChecklistItemActionType Action,
    int? SelectedSourceId);
