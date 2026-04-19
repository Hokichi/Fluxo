using System.Collections.ObjectModel;
using System.Globalization;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

public partial class SavingGoalsPanelVM : ObservableRecipient, IRecipient<DashboardDataInvalidatedMessage>
{
    private readonly IMapper _mapper;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserSettingsRepository _userSettingsRepository;
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

    [ObservableProperty]
    private bool _hasSavingGoals;

    [ObservableProperty]
    private bool _hasMultipleSavingGoals;

    [ObservableProperty]
    private int _currentGoalIndex = -1;

    [ObservableProperty]
    private SavingGoalVM? _currentGoal;

    [ObservableProperty]
    private int _navigationDirection;

    public ObservableCollection<SavingGoalVM> SavingGoals { get; } = [];
    public ObservableCollection<SavingGoalCarouselDotVM> GoalDots { get; } = [];

    public void Receive(DashboardDataInvalidatedMessage message)
    {
        if (!message.Value.HasFlag(DashboardDataInvalidationScope.SavingGoals))
            return;

        _ = ReloadFromServicesAsync();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadSavingGoalSettingsAsync(cancellationToken);

        var savingGoalDtos = _mapper.Map<IReadOnlyList<SavingGoalDto>>(
            await _unitOfWork.SavingGoals.GetAllAsync(cancellationToken));
        var savingGoals = _mapper.Map<IReadOnlyList<SavingGoalVM>>(savingGoalDtos);

        var previousGoalId = CurrentGoal?.Id;

        SavingGoals.Clear();

        foreach (var goal in savingGoals.Where(goal =>
                     goal.ProgressRatio < 1m &&
                     !_hiddenSavingGoalIds.Contains(goal.Id) &&
                     !_disabledSavingGoalIds.Contains(goal.Id)))
            SavingGoals.Add(goal);

        HasSavingGoals = SavingGoals.Count > 0;
        HasMultipleSavingGoals = SavingGoals.Count > 1;

        if (!HasSavingGoals)
        {
            CurrentGoalIndex = -1;
            CurrentGoal = null;
            NavigationDirection = 0;
            GoalDots.Clear();
            return;
        }

        var initialIndex = previousGoalId.HasValue
            ? SavingGoals.ToList().FindIndex(goal => goal.Id == previousGoalId.Value)
            : -1;

        SetCurrentGoalByIndex(initialIndex >= 0 ? initialIndex : 0, animateDirection: 0);
    }

    [RelayCommand]
    public void NavigatePrevious()
    {
        NavigateByOffset(-1, slideDirection: 1);
    }

    [RelayCommand]
    public void NavigateNext()
    {
        NavigateByOffset(1, slideDirection: -1);
    }

    private async Task ReloadFromServicesAsync()
    {
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

    private void NavigateByOffset(int offset, int slideDirection)
    {
        if (SavingGoals.Count == 0)
            return;

        if (SavingGoals.Count == 1)
        {
            SetCurrentGoalByIndex(0, animateDirection: 0);
            return;
        }

        var normalizedCurrentIndex = CurrentGoalIndex >= 0 ? CurrentGoalIndex : 0;
        var targetIndex = (normalizedCurrentIndex + offset + SavingGoals.Count) % SavingGoals.Count;
        SetCurrentGoalByIndex(targetIndex, slideDirection);
    }

    private void SetCurrentGoalByIndex(int index, int animateDirection)
    {
        if (SavingGoals.Count == 0)
        {
            CurrentGoalIndex = -1;
            CurrentGoal = null;
            NavigationDirection = 0;
            return;
        }

        if (index < 0 || index >= SavingGoals.Count)
            index = 0;

        CurrentGoalIndex = index;
        NavigationDirection = animateDirection;
        CurrentGoal = SavingGoals[index];
        UpdateGoalDots();
    }

    private void RebuildGoalDots()
    {
        GoalDots.Clear();

        for (var index = 0; index < SavingGoals.Count; index++)
        {
            GoalDots.Add(new SavingGoalCarouselDotVM
            {
                IsActive = index == CurrentGoalIndex
            });
        }
    }

    private void UpdateGoalDots()
    {
        if (GoalDots.Count != SavingGoals.Count)
        {
            RebuildGoalDots();
            return;
        }

        for (var index = 0; index < GoalDots.Count; index++)
            GoalDots[index].IsActive = index == CurrentGoalIndex;
    }
}
