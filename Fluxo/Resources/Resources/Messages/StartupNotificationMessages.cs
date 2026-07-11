using CommunityToolkit.Mvvm.Messaging.Messages;
using Fluxo.Services.Notifications;

namespace Fluxo.Resources.Resources.Messages;

public enum NotificationEntityKind { Account, RecurringTransaction, SavingGoal }

public sealed class StartupNotificationStateChangedMessage(StartupNotificationEvaluation value)
    : ValueChangedMessage<StartupNotificationEvaluation>(value);

public sealed class NotificationEntityCreatedMessage(NotificationEntityKind kind, int entityId)
    : ValueChangedMessage<(NotificationEntityKind Kind, int EntityId)>((kind, entityId));

public sealed class NotificationProcessingRequestedMessage(string category, IReadOnlyList<int> entityIds)
    : ValueChangedMessage<(string Category, IReadOnlyList<int> EntityIds)>((category, entityIds));
