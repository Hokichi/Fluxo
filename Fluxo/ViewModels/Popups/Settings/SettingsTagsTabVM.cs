using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;
using System.Globalization;

namespace Fluxo.ViewModels.Popups.Settings;

public readonly record struct SettingsTagDialogRequest(
    AddTagVM ViewModel,
    Func<string, string, string, Task<SettingsOperationResult>> SaveTagAsync);

public partial class SettingsTagsTabVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IMessenger _messenger;
    private readonly IAppDataService _appData;

    public SettingsTagsTabVM(MainVM mainViewModel, IAppDataService appData, IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<TagVM> Tags { get; } = [];

    public async Task LoadAsync()
    {
        await RefreshTagsAsync();
    }

    public void RequestAddTagDialog()
    {
        _messenger.Send(new SettingsDialogRequestedMessage(
            new SettingsDialogRequest(
                SettingsDialogRequestType.AddTag,
                new SettingsTagDialogRequest(CreateAddTagViewModel(), CreateTagAsync))));
    }

    public AddTagVM CreateAddTagViewModel()
    {
        return new AddTagVM();
    }

    public async Task<AddTagVM?> CreateEditTagViewModelAsync(int tagId)
    {
        var tag = await _appData.GetTagByIdAsync(tagId);
        if (tag is null || tag.IsSystemTag)
            return null;

        return new AddTagVM(tag.Id, tag.Name, tag.HexCode, tag.SpendingLimit);
    }

    public async Task OpenEditTagAsync(int tagId)
    {
        var viewModel = await CreateEditTagViewModelAsync(tagId);
        if (viewModel is null)
            return;

        _messenger.Send(new SettingsDialogRequestedMessage(
            new SettingsDialogRequest(
                SettingsDialogRequestType.AddTag,
                new SettingsTagDialogRequest(viewModel, (name, hexCode, spendingLimit) => UpdateTagAsync(tagId, name, hexCode, spendingLimit)))));

        await RefreshTagsAsync();
    }

    public async Task RefreshTagsAsync()
    {
        SettingsShared.ReplaceCollection(Tags, (await _appData.GetTagsByCountDescendingAsync())
            .Where(item => !item.Tag.IsSystemTag)
            .Select(item => new TagVM
            {
                Id = item.Tag.Id,
                Name = item.Tag.Name,
                HexCode = item.Tag.HexCode,
                IsSystemTag = item.Tag.IsSystemTag,
                SpendingLimit = item.Tag.SpendingLimit
            }));
    }

    public async Task<SettingsOperationResult> CreateTagAsync(string name, string hexCode, string spendingLimitText)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        var normalizedHexCode = NormalizeHexColor(hexCode);

        if (trimmedName.Length == 0)
            return SettingsOperationResult.Failure("Please enter a tag name.");

        if (!IsHexColor(normalizedHexCode))
            return SettingsOperationResult.Failure("Please choose a valid tag color.");

        if (!TryParseSpendingLimit(spendingLimitText, out var spendingLimit, out var spendingLimitError))
            return SettingsOperationResult.Failure(spendingLimitError);

        try
        {
            var existingTags = await _appData.GetTagsAsync();
            if (existingTags.Any(tag => string.Equals(tag.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
                return SettingsOperationResult.Failure($"A tag named \"{trimmedName}\" already exists.");

            await _appData.AddTagAsync(new Tag
            {
                Name = trimmedName,
                HexCode = normalizedHexCode,
                SpendingLimit = spendingLimit
            });

            await _appData.SaveChangesAsync();
            _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.Tags));
            _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.All));
            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshTagsAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to create this tag.");
            return SettingsOperationResult.Failure(
                FluxoLogManager.CreateFailureMessage("create tag"));
        }
    }

    public Task<SettingsOperationResult> CreateTagAsync(string name, string hexCode) =>
        CreateTagAsync(name, hexCode, string.Empty);

    public async Task<SettingsOperationResult> DeleteTagAsync(TagVM tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        try
        {
            var persistedTag = await _appData.GetTagByIdAsync(tag.Id);
            if (persistedTag is null)
                return SettingsOperationResult.Failure("That tag could not be found anymore.");

            var allTransactions = await _appData.GetTransactionsAsync();
            var linkedExpenses = allTransactions.Where(transaction => transaction.TagId == persistedTag.Id).ToList();
            if (linkedExpenses.Count > 0)
                return SettingsOperationResult.Failure(
                    $"{persistedTag.Name} is still assigned to one or more expenses, so it can't be deleted yet.");

            var snapshot = TagMemorySnapshot.Create(persistedTag);
            _appData.RemoveTag(persistedTag);
            await _appData.SaveChangesAsync();

            SettingsShared.RecordActions([new DeleteTagMemoryAction(snapshot)], _messenger);
            _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.Tags));
            _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.All));
            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshTagsAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to delete this tag.");
            return SettingsOperationResult.Failure(
                FluxoLogManager.CreateFailureMessage("delete tag"));
        }
    }

    public async Task<SettingsOperationResult> UpdateTagAsync(int tagId, string name, string hexCode, string spendingLimitText)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        var normalizedHexCode = NormalizeHexColor(hexCode);

        if (trimmedName.Length == 0)
            return SettingsOperationResult.Failure("Please enter a tag name.");

        if (!IsHexColor(normalizedHexCode))
            return SettingsOperationResult.Failure("Please choose a valid tag color.");

        if (!TryParseSpendingLimit(spendingLimitText, out var spendingLimit, out var spendingLimitError))
            return SettingsOperationResult.Failure(spendingLimitError);

        try
        {
            var tag = await _appData.GetTagByIdAsync(tagId);
            if (tag is null)
                return SettingsOperationResult.Failure("That tag could not be found anymore.");

            if (tag.IsSystemTag)
                return SettingsOperationResult.Failure("System tags can't be edited.");

            var existingTags = await _appData.GetTagsAsync();
            if (existingTags.Any(tag =>
                    tag.Id != tag.Id &&
                    string.Equals(tag.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
                return SettingsOperationResult.Failure($"A tag named \"{trimmedName}\" already exists.");

            var beforeSnapshot = TagMemorySnapshot.Create(tag);
            var hasNameChanged = !string.Equals(tag.Name, trimmedName, StringComparison.Ordinal);
            var hasHexChanged = !string.Equals(tag.HexCode, normalizedHexCode, StringComparison.OrdinalIgnoreCase);
            var hasSpendingLimitChanged = tag.SpendingLimit != spendingLimit;
            if (!hasNameChanged && !hasHexChanged && !hasSpendingLimitChanged)
                return SettingsOperationResult.Success();

            tag.Name = trimmedName;
            tag.HexCode = normalizedHexCode;
            tag.SpendingLimit = spendingLimit;
            _appData.UpdateTag(tag);
            await _appData.SaveChangesAsync();

            var afterSnapshot = TagMemorySnapshot.Create(tag);
            SettingsShared.RecordActions([new EditTagMemoryAction(beforeSnapshot, afterSnapshot)], _messenger);
            _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.Tags));
            _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.All));
            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshTagsAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to update this tag.");
            return SettingsOperationResult.Failure(
                FluxoLogManager.CreateFailureMessage("update tag"));
        }
    }

    public Task<SettingsOperationResult> UpdateTagAsync(int tagId, string name, string hexCode) =>
        UpdateTagAsync(tagId, name, hexCode, string.Empty);

    private static string NormalizeHexColor(string hexCode)
    {
        var normalized = (hexCode ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();
        return $"#{normalized}";
    }

    private static bool IsHexColor(string hexCode)
    {
        var normalized = NormalizeHexColor(hexCode);
        return normalized.Length == 7 &&
               normalized[0] == '#' &&
               normalized.Skip(1).All(static character => char.IsDigit(character) ||
                                                          (character >= 'A' && character <= 'F'));
    }

    private static bool TryParseSpendingLimit(string? value, out decimal? spendingLimit, out string errorMessage)
    {
        spendingLimit = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < 0m)
        {
            errorMessage = "Please enter a valid spending limit.";
            return false;
        }

        spendingLimit = parsed == 0m ? null : parsed;
        return true;
    }
}

