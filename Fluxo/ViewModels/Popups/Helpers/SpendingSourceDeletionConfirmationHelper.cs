using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.ViewModels.Popups.Helpers;

public static class SpendingSourceDeletionConfirmationHelper
{
    public static async Task<string> BuildDeleteConfirmationMessageAsync(
        IAppDataService appData,
        int sourceId,
        string? fallbackSourceName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appData);

        var sources = await appData.GetSpendingSourcesAsync(cancellationToken);
        var source = sources.FirstOrDefault(item => item.Id == sourceId);
        var sourceName = string.IsNullOrWhiteSpace(source?.Name) ? fallbackSourceName ?? "this source" : source.Name;
        var isOnlyFunctioningSource = source is not null && IsOnlyFunctioningSource(sources, source.Id);

        return BuildDeleteConfirmationMessage(sourceName, isOnlyFunctioningSource);
    }

    public static string BuildDeleteConfirmationMessage(string sourceName, bool isOnlyFunctioningSource)
    {
        if (isOnlyFunctioningSource)
            return $"{sourceName} is the only available source. Deleting it will lock the application. Proceed to delete {sourceName} and all of its data?\n**THIS ACTION CANNOT BE UNDONE**";

        return $"Delete {sourceName} and all of its data?\n**THIS ACTION CANNOT BE UNDONE**";
    }

    public static bool IsOnlyFunctioningSource(IEnumerable<SpendingSource> sources, int sourceId)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var functioningSourceIds = sources
            .Where(source => IsFunctioning(source.SpendingSourceType, source.IsEnabled, source.Balance, source.AccountLimit))
            .Select(source => source.Id)
            .ToList();

        return functioningSourceIds.Count == 1 && functioningSourceIds[0] == sourceId;
    }

    public static bool IsFunctioning(SpendingSourceType sourceType, bool isEnabled, decimal balance, decimal accountLimit)
    {
        if (!isEnabled)
            return false;

        return sourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? accountLimit > 0m
            : balance > 0m;
    }
}
