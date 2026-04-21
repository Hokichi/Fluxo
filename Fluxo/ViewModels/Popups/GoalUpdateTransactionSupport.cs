using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;

namespace Fluxo.ViewModels.Popups;

public static class GoalUpdateTransactionSupport
{
    public const string GoalUpdateTagName = "Goal Update";
    public const string GoalUpdateTagColor = "#aed4e1";

    public static bool IsEligibleGoalSourceType(SpendingSourceType sourceType)
    {
        return sourceType is SpendingSourceType.Cash or SpendingSourceType.Checking;
    }

    public static async Task<ExpenseTag> ResolveGoalUpdateTagAsync(IUnitOfWork unitOfWork)
    {
        var tags = await unitOfWork.ExpenseTags.GetAllAsync();
        var existingTag = tags.FirstOrDefault(tag =>
            string.Equals(tag.Name, GoalUpdateTagName, StringComparison.OrdinalIgnoreCase));
        if (existingTag is not null)
            return existingTag;

        var goalUpdateTag = new ExpenseTag
        {
            Name = GoalUpdateTagName,
            HexCode = GoalUpdateTagColor
        };

        await unitOfWork.ExpenseTags.AddAsync(goalUpdateTag);
        await unitOfWork.SaveChangesAsync();
        return goalUpdateTag;
    }
}
