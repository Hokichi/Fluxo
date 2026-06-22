using Fluxo.Core.Enums;

namespace Fluxo.Core.Budgeting;

public sealed class BudgetAllocationBalancer
{
    private BudgetAllocationSegment? _lastRedistributionSegment;
    private bool _lastRedistributionIncreasedOtherBuckets;
    private bool _nextOddRemainderUsesPrimaryBucket = true;

    public (int Needs, int Wants, int Invest) Balance(
        int needs,
        int wants,
        int invest,
        BudgetAllocationSegment changedSegment,
        int requestedValue)
    {
        var values = new Dictionary<BudgetAllocationSegment, int>
        {
            [BudgetAllocationSegment.Needs] = needs,
            [BudgetAllocationSegment.Wants] = wants,
            [BudgetAllocationSegment.Invest] = invest
        };

        var value = Math.Clamp(requestedValue, 0, 100);
        var oldValue = values[changedSegment];
        if (oldValue == value)
            return (needs, wants, invest);

        values[changedSegment] = value;
        var delta = value - oldValue;
        var increaseOtherBuckets = delta < 0;
        ResetOddRemainderSequenceIfNeeded(changedSegment, increaseOtherBuckets);
        RedistributeDelta(values, changedSegment, delta, _nextOddRemainderUsesPrimaryBucket);
        if (Math.Abs(delta) % 2 == 1)
            _nextOddRemainderUsesPrimaryBucket = !_nextOddRemainderUsesPrimaryBucket;

        return (
            values[BudgetAllocationSegment.Needs],
            values[BudgetAllocationSegment.Wants],
            values[BudgetAllocationSegment.Invest]);
    }

    private static void RedistributeDelta(
        IDictionary<BudgetAllocationSegment, int> values,
        BudgetAllocationSegment changedSegment,
        int delta,
        bool oddRemainderUsesPrimaryBucket)
    {
        if (delta == 0)
            return;

        var increaseOtherBuckets = delta < 0;
        var remaining = Math.Abs(delta);
        var distributionOrder = GetDistributionOrder(changedSegment, increaseOtherBuckets);
        var primaryShare = remaining / 2;
        var secondaryShare = remaining / 2;
        if (remaining % 2 == 1)
        {
            if (oddRemainderUsesPrimaryBucket)
                primaryShare++;
            else
                secondaryShare++;
        }

        var shares = new[] { primaryShare, secondaryShare };

        for (var index = 0; index < distributionOrder.Count; index++)
            remaining -= ApplyShare(values, distributionOrder[index], shares[index], increaseOtherBuckets);

        var spillIndex = 0;
        while (remaining > 0 && spillIndex < distributionOrder.Count)
        {
            var applied = ApplyShare(values, distributionOrder[spillIndex], remaining, increaseOtherBuckets);
            remaining -= applied;
            spillIndex = applied == 0 ? spillIndex + 1 : spillIndex;
        }
    }

    private static int ApplyShare(
        IDictionary<BudgetAllocationSegment, int> values,
        BudgetAllocationSegment segment,
        int requestedShare,
        bool increase)
    {
        if (requestedShare <= 0)
            return 0;

        var currentValue = values[segment];
        var capacity = increase ? 100 - currentValue : currentValue;
        var applied = Math.Min(requestedShare, capacity);
        values[segment] = increase ? currentValue + applied : currentValue - applied;
        return applied;
    }

    private static IReadOnlyList<BudgetAllocationSegment> GetDistributionOrder(
        BudgetAllocationSegment changedSegment,
        bool increase)
    {
        var ordered = new[]
        {
            BudgetAllocationSegment.Needs,
            BudgetAllocationSegment.Wants,
            BudgetAllocationSegment.Invest
        }.Where(segment => segment != changedSegment);

        return (increase
                ? ordered.OrderBy(GetPriority)
                : ordered.OrderByDescending(GetPriority))
            .ToList();
    }

    private static int GetPriority(BudgetAllocationSegment segment) => segment switch
    {
        BudgetAllocationSegment.Needs => 0,
        BudgetAllocationSegment.Wants => 1,
        BudgetAllocationSegment.Invest => 2,
        _ => 3
    };

    private void ResetOddRemainderSequenceIfNeeded(
        BudgetAllocationSegment segment,
        bool increaseOtherBuckets)
    {
        if (_lastRedistributionSegment == segment &&
            _lastRedistributionIncreasedOtherBuckets == increaseOtherBuckets)
        {
            return;
        }

        _lastRedistributionSegment = segment;
        _lastRedistributionIncreasedOtherBuckets = increaseOtherBuckets;
        _nextOddRemainderUsesPrimaryBucket = true;
    }
}
