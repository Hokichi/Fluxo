using System.Collections.ObjectModel;
using System.Globalization;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Messages;

namespace Fluxo.ViewModels.Shell;

public partial class SavingGoalsPanelVM : ObservableRecipient, IRecipient<DashboardDataInvalidatedMessage>
{
    private readonly IMapper? _mapper;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly IUnitOfWork? _unitOfWork;
    private readonly IUserSettingsRepository? _userSettingsRepository;
    private readonly HashSet<int> _disabledSavingGoalIds = [];
    private readonly HashSet<int> _hiddenSavingGoalIds = [];

    public SavingGoalsPanelVM(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IUserSettingsRepository userSettingsRepository,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _userSettingsRepository = userSettingsRepository;
        IsActive = true;
    }

    public SavingGoalsPanelVM(IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        IsActive = true;
    }

    [ObservableProperty]
    private bool _hasSavingGoals;

    public ObservableCollection<SavingGoalVM> SavingGoals { get; } = [];

    public void Receive(DashboardDataInvalidatedMessage message)
    {
        if (!message.Value.HasFlag(DashboardDataInvalidationScope.SavingGoals))
            return;

        _ = ReloadSnapshotFromServicesAsync();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_unitOfWork is null || _mapper is null || _userSettingsRepository is null)
            return;

        await LoadSavingGoalSettingsAsync(cancellationToken);

        var savingGoalDtos = _mapper.Map<IReadOnlyList<SavingGoalDto>>(
            await _unitOfWork.SavingGoals.GetAllAsync(cancellationToken));
        var savingGoals = _mapper.Map<IReadOnlyList<SavingGoalVM>>(savingGoalDtos);

        LoadSnapshot(savingGoals);
    }

    public void LoadSnapshot(IEnumerable<SavingGoalVM> savingGoals)
    {
        ArgumentNullException.ThrowIfNull(savingGoals);

        SavingGoals.Clear();

        foreach (var goal in savingGoals.Where(goal =>
                     goal.ProgressRatio < 1m &&
                     !_hiddenSavingGoalIds.Contains(goal.Id) &&
                     !_disabledSavingGoalIds.Contains(goal.Id)))
            SavingGoals.Add(goal);

        HasSavingGoals = SavingGoals.Count > 0;
    }

    private async Task ReloadSnapshotFromServicesAsync()
    {
        if (_unitOfWork is null || _mapper is null || _userSettingsRepository is null)
            return;

        await _reloadGate.WaitAsync();

        try
        {
            await LoadAsync();
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private async Task LoadSavingGoalSettingsAsync(CancellationToken cancellationToken)
    {
        if (_userSettingsRepository is null)
            return;

        var settings = await _userSettingsRepository.GetAllAsync(cancellationToken);
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        _hiddenSavingGoalIds.Clear();
        _hiddenSavingGoalIds.UnionWith(ParseIdSet(settingsByName, UserSettingNames.HiddenSavingGoalIds));

        _disabledSavingGoalIds.Clear();
        _disabledSavingGoalIds.UnionWith(ParseIdSet(settingsByName, UserSettingNames.DisabledSavingGoalIds));
    }

    private static IReadOnlyCollection<int> ParseIdSet(IReadOnlyDictionary<string, string> settings, string name)
    {
        if (!settings.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id
                : -1)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }
}
