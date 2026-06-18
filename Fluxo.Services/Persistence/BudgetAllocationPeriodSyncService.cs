using Fluxo.Core.Budgeting;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class BudgetAllocationPeriodSyncService(Func<DateTime>? todayProvider = null)
    : IBudgetAllocationPeriodSyncService
{
    private readonly Func<DateTime> _todayProvider = todayProvider ?? (() => DateTime.Today);

    public async Task SyncAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var allocation = await unitOfWork.BudgetAllocation.GetAsync(cancellationToken);
        if (allocation is null)
        {
            allocation = new BudgetAllocation();
            await unitOfWork.BudgetAllocation.AddAsync(allocation, cancellationToken);
        }

        var today = _todayProvider().Date;
        var clampedStart = BudgetAllocationPeriodRules.ClampPeriodStart(
            allocation.AllocationPeriod,
            allocation.PeriodStart);
        var currentIndex = BudgetAllocationPeriodRules.ResolveCurrentPeriodIndex(
            allocation.AllocationPeriod,
            today);
        var changed = false;
        if (allocation.PeriodStart != clampedStart)
        {
            allocation.PeriodStart = clampedStart;
            changed = true;
        }

        if (allocation.CurrentPeriodIndex != currentIndex)
        {
            allocation.CurrentPeriodIndex = currentIndex;
            changed = true;
        }

        if (!changed)
            return;

        unitOfWork.BudgetAllocation.Update(allocation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
