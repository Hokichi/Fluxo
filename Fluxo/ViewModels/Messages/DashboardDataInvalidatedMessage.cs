using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.ViewModels.Messages;

[Flags]
public enum DashboardDataInvalidationScope
{
    None = 0,
    Budget = 1 << 0,
    SavingGoals = 1 << 1,
    Notifications = 1 << 2,
    All = Budget | SavingGoals | Notifications
}

public sealed class DashboardDataInvalidatedMessage(DashboardDataInvalidationScope value)
    : ValueChangedMessage<DashboardDataInvalidationScope>(value);
