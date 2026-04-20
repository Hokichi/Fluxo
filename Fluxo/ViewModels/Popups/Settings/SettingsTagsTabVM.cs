using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsTagsTabVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IMessenger _messenger;
    private readonly IUnitOfWork _unitOfWork;

    public SettingsTagsTabVM(MainVM mainViewModel, IUnitOfWork unitOfWork, IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<ExpenseTagVM> Tags { get; } = [];

    public async Task LoadAsync()
    {
        await RefreshTagsAsync();
    }

    public void RequestAddTagDialog()
    {
        _messenger.Send(new SettingsDialogRequestedMessage(
            new SettingsDialogRequest(
                SettingsDialogRequestType.AddTag,
                this)));
    }

    public async Task RefreshTagsAsync()
    {
        SettingsShared.ReplaceCollection(Tags, (await _unitOfWork.ExpenseTags.GetTagsByCountDescendingAsync())
            .Select(item => new ExpenseTagVM
            {
                Id = item.Tag.Id,
                Name = item.Tag.Name,
                HexCode = item.Tag.HexCode,
                IconName = item.Tag.IconName
            }));
    }

    public async Task<SettingsOperationResult> CreateTagAsync(string name, string hexCode)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        var normalizedHexCode = NormalizeHexColor(hexCode);

        if (trimmedName.Length == 0)
            return SettingsOperationResult.Failure("Please enter a tag name.");

        if (!IsHexColor(normalizedHexCode))
            return SettingsOperationResult.Failure("Please choose a valid tag color.");

        try
        {
            var existingTags = await _unitOfWork.ExpenseTags.GetAllAsync();
            if (existingTags.Any(tag => string.Equals(tag.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
                return SettingsOperationResult.Failure($"A tag named \"{trimmedName}\" already exists.");

            await _unitOfWork.ExpenseTags.AddAsync(new ExpenseTag
            {
                Name = trimmedName,
                HexCode = normalizedHexCode,
                IconName = string.Empty
            });

            await _unitOfWork.SaveChangesAsync();
            _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.Tags));
            _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.All));
            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshTagsAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to create this tag.\n\n{exception.Message}");
        }
    }

    public async Task<SettingsOperationResult> DeleteTagAsync(ExpenseTagVM tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        try
        {
            var expenseTag = await _unitOfWork.ExpenseTags.GetByIdAsync(tag.Id);
            if (expenseTag is null)
                return SettingsOperationResult.Failure("That tag could not be found anymore.");

            var allExpenses = await _unitOfWork.Expenses.GetAllAsync();
            var linkedExpenses = allExpenses.Where(e => e.ExpenseTagId == tag.Id).ToList();
            if (linkedExpenses.Count > 0)
                return SettingsOperationResult.Failure(
                    $"{tag.Name} is still assigned to one or more expenses, so it can't be deleted yet.");

            var snapshot = ExpenseTagMemorySnapshot.Create(expenseTag);
            _unitOfWork.ExpenseTags.Remove(expenseTag);
            await _unitOfWork.SaveChangesAsync();

            SettingsShared.RecordActions([new DeleteExpenseTagMemoryAction(snapshot)], _messenger);
            _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.Tags));
            _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.All));
            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshTagsAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to delete this tag.\n\n{exception.Message}");
        }
    }

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
}
