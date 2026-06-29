using Fluxo.Core.Budgeting;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Tests.TestSupport;
using Fluxo.ViewModels.Popups.Settings;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Settings;

public sealed class SettingsTagsTabTests
{
    [Fact]
    public void View_UsesApprovedCardLayoutAndExplicitActions()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsTagsTab.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File(
            "Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsTagsTab.xaml.cs"));

        Assert.Contains("<customControls:SpacedUniformGrid", xaml);
        Assert.Contains("Columns=\"2\"", xaml);
        Assert.Contains("HorizontalGap=\"16\"", xaml);
        Assert.Contains("VerticalGap=\"16\"", xaml);
        Assert.Contains("Height=\"120\"", xaml);
        Assert.Contains("Style=\"{StaticResource CardBorderStyle}\"", xaml);
        Assert.Contains("ButtonIcon=\"{StaticResource Pen}\"", xaml);
        Assert.Contains("ButtonIcon=\"{StaticResource Delete}\"", xaml);
        Assert.Equal(2, xaml.Split("DefaultBackground=\"{StaticResource Brush.BalloonButton.Background.Neutral}\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("HoveredBackground=\"{StaticResource Brush.BalloonButton.Background.Neutral.Hovered}\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("No tags yet — add one to get started.", xaml);
        Assert.DoesNotContain("Click the color dot", xaml);
        Assert.Contains("OnEditTagClick", codeBehind);
        Assert.Contains("OnDeleteTagClick", codeBehind);
        Assert.DoesNotContain("OnTagMouseDoubleClick", codeBehind);
        Assert.DoesNotContain("IsDotClick", codeBehind);
    }

    [Fact]
    public void CreateCards_UsesOnlyEffectiveExpensesFromCurrentPeriod()
    {
        var today = DateTime.Today;
        var allocation = new BudgetAllocation
        {
            AllocationPeriod = AllocationPeriod.Monthly,
            PeriodStart = 1
        };
        var period = BudgetAllocationCalculator.ResolveCurrentPeriod(
            allocation.AllocationPeriod,
            today,
            allocation.PeriodStart);
        var food = new Tag { Id = 1, Name = "Food", HexCode = "#A983FF", SpendingLimit = 100m };
        var other = new Tag { Id = 2, Name = "Other", HexCode = "#75AEF5", SpendingLimit = 100m };
        var system = new Tag { Id = 3, Name = "System", HexCode = "#FFFFFF", IsSystemTag = true };
        var transactions = new List<Transaction>
        {
            Expense(1, 10m, today, food.Id),
            Expense(2, 20m, today, food.Id, excluded: true),
            Expense(3, 30m, today, food.Id, deleted: true),
            new() { Id = 4, Type = TransactionType.Income, Amount = 40m, OccurredOn = today, TagId = food.Id },
            Expense(5, 50m, today, other.Id),
            Expense(6, 60m, period.Start.AddDays(-1), food.Id),
            Expense(7, 70m, period.End.AddDays(1), food.Id)
        };

        var cards = SettingsTagsTabVM.CreateCards(
            new List<(Tag Tag, int Count)> { (food, 7), (other, 1), (system, 0) },
            transactions,
            allocation,
            today);

        Assert.Equal(new[] { food.Id, other.Id }, cards.Select(card => card.Id));
        Assert.Equal(10m, cards[0].Spent);
        Assert.Equal(50m, cards[1].Spent);
    }

    [Theory]
    [InlineData(74, SettingsTagSpendingState.Success)]
    [InlineData(75, SettingsTagSpendingState.Warning)]
    [InlineData(100, SettingsTagSpendingState.Warning)]
    [InlineData(101, SettingsTagSpendingState.Danger)]
    public void Create_LimitedTagUsesApprovedThresholds(int spent, SettingsTagSpendingState expected)
    {
        var card = SettingsTagCardVM.Create(
            new Tag { Id = 1, Name = "Food", HexCode = "#A983FF", SpendingLimit = 100m },
            spent);

        Assert.Equal(expected, card.SpendingState);
        Assert.Equal($"{spent}%", card.PercentageText);
        Assert.Equal((double)Math.Min(spent, 100), card.ProgressPercentage);
    }

    [Fact]
    public void Create_OverLimitTagClampsBarAndShowsUnsignedOverage()
    {
        var card = SettingsTagCardVM.Create(
            new Tag { Id = 1, Name = "Grocery", HexCode = "#FFC22C", SpendingLimit = 300m },
            310m);

        Assert.Equal("310", card.SpentText);
        Assert.Equal("of 300", card.LimitText);
        Assert.Equal("10 over", card.RemainderText);
        Assert.Equal("103%", card.PercentageText);
        Assert.Equal(100d, card.ProgressPercentage);
        Assert.Equal(SettingsTagSpendingState.Danger, card.SpendingState);
    }

    [Fact]
    public void Create_NoLimitTagUsesFullSuccessBarAndInfinityBadge()
    {
        var card = SettingsTagCardVM.Create(
            new Tag { Id = 1, Name = "Personal Needs", HexCode = "#65DDB5" },
            28m);

        Assert.False(card.HasSpendingLimit);
        Assert.Equal("28", card.SpentText);
        Assert.Empty(card.LimitText);
        Assert.Empty(card.RemainderText);
        Assert.Equal("∞", card.PercentageText);
        Assert.Equal(100d, card.ProgressPercentage);
        Assert.Equal(SettingsTagSpendingState.Success, card.SpendingState);
    }

    private static Transaction Expense(
        int id,
        decimal amount,
        DateTime occurredOn,
        int tagId,
        bool excluded = false,
        bool deleted = false) =>
        new()
        {
            Id = id,
            Type = TransactionType.Expense,
            Amount = amount,
            OccurredOn = occurredOn,
            TagId = tagId,
            IsExcludedFromBudget = excluded,
            IsForDeletion = deleted
        };
}
