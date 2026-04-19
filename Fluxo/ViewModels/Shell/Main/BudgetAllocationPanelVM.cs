using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Messages;

namespace Fluxo.ViewModels.Shell.Main;

public partial class BudgetAllocationPanelVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<DashboardDataInvalidatedMessage>
{
    private const decimal InvestThreshold = 0.2m;
    private const decimal NeedsThreshold = 0.5m;
    private const decimal WantsThreshold = 0.3m;

    private readonly IExpenseLogService _expenseLogService;
    private readonly IMapper _mapper;
    private readonly ObservableCollection<ExpenseLogVM> _investSource = [];
    private readonly ObservableCollection<ExpenseLogVM> _needsSource = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly ITagService _tagService;
    private readonly ObservableCollection<ExpenseLogVM> _wantsSource = [];
    private readonly ObservableCollection<SpendingSourceVM> _spendingSources = [];

    private List<ExpenseLogVM> _allExpenseLogs = [];
    private bool _isSynchronizingTagSelections;
    private (DateTime From, DateTime To)? _selectedRange;

    public BudgetAllocationPanelVM(
        IExpenseLogService expenseLogService,
        ISpendingSourceService spendingSourceService,
        ITagService tagService,
        IMapper mapper,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _tagService = tagService;
        _mapper = mapper;

        Initialize();
    }

    [ObservableProperty]
    private decimal _totalSpent;

    [ObservableProperty]
    private int _dailyAllowance;

    [ObservableProperty]
    private decimal _needsAvailable;

    [ObservableProperty]
    private decimal _wantsAvailable;

    [ObservableProperty]
    private decimal _investAvailable;

    [ObservableProperty]
    private decimal _needsSpent;

    [ObservableProperty]
    private decimal _wantsSpent;

    [ObservableProperty]
    private decimal _investSpent;

    [ObservableProperty]
    private int _needsPercentage;

    [ObservableProperty]
    private int _wantsPercentage;

    [ObservableProperty]
    private int _investPercentage;

    [ObservableProperty]
    private ICollectionView _needs = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty]
    private ICollectionView _wants = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty]
    private ICollectionView _invest = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty]
    private bool _isNeedsEmpty;

    [ObservableProperty]
    private bool _isWantsEmpty;

    [ObservableProperty]
    private bool _isInvestEmpty;

    [ObservableProperty]
    private ObservableCollection<ExpenseTagVM> _tags = [];

    [ObservableProperty]
    private ObservableCollection<ExpenseTagVM> _otherTags = [];

    [ObservableProperty]
    private ExpenseTagVM? _selectedTag;

    [ObservableProperty]
    private ExpenseTagVM? _selectedVisibleTag;

    [ObservableProperty]
    private ExpenseTagVM? _selectedOtherTag;

    [ObservableProperty]
    private int? _selectedSpendingSourceId;

    public bool HasOtherTags => OtherTags.Count > 0;

    public bool IsSelectedTagInOtherTags => SelectedOtherTag is not null;

    public ObservableCollection<SpendingSourceVM> SpendingSources => _spendingSources;

    public decimal TotalIncomeAmount => _spendingSources.Sum(source => source.Balance);

    public IReadOnlyList<ExpenseLogVM> GetAllExpenseLogs() => _allExpenseLogs.ToList();

    public void Receive(DateRangeSelectionChangedMessage message)
    {
        _selectedRange = message.Value;
        ApplyVisibleExpenseLogs();
    }

    public void Receive(AllTimeViewModeMessage message)
    {
        _selectedRange = null;
        ApplyVisibleExpenseLogs();
    }

    public void Receive(DashboardDataInvalidatedMessage message)
    {
        if (!message.Value.HasFlag(DashboardDataInvalidationScope.Budget))
            return;

        _ = ReloadFromServicesAsync();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync(cancellationToken));
        var spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
            await _spendingSourceService.GetAllAsync(cancellationToken));
        var tags = _mapper.Map<IReadOnlyList<ExpenseTagVM>>(
            await _tagService.GetAllAsync(cancellationToken));

        _allExpenseLogs = expenseLogs
            .Where(log => !log.IsForDeletion)
            .OrderByDescending(log => log.DeductedOn)
            .ToList();

        _spendingSources.Clear();
        foreach (var source in spendingSources)
            _spendingSources.Add(source);

        if (SelectedSpendingSourceId is int selectedId &&
            _spendingSources.All(source => source.Id != selectedId))
            SelectedSpendingSourceId = null;
        else
            SynchronizeSpendingSourceSelections(SelectedSpendingSourceId);

        LoadTags(tags);
        RefreshBudgetMetrics();
        ApplyVisibleExpenseLogs();
    }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
        SynchronizeTagSelections(value);
        OnPropertyChanged(nameof(IsSelectedTagInOtherTags));
        RefreshExpenseViews();
    }

    partial void OnSelectedSpendingSourceIdChanged(int? value)
    {
        SynchronizeSpendingSourceSelections(value);
        RefreshExpenseViews();
    }

    partial void OnSelectedVisibleTagChanged(ExpenseTagVM? value)
    {
        if (_isSynchronizingTagSelections)
            return;

        SelectedTag = value;
    }

    partial void OnSelectedOtherTagChanged(ExpenseTagVM? value)
    {
        if (_isSynchronizingTagSelections)
            return;

        SelectedTag = value;
    }

    [RelayCommand]
    private void ClearSelectedTag()
    {
        SelectedTag = null;
    }

    public void ToggleSelectedSpendingSource(SpendingSourceVM? source)
    {
        var selectedId = source?.Id;
        if (selectedId is null)
            return;

        SelectedSpendingSourceId = SelectedSpendingSourceId == selectedId
            ? null
            : selectedId;
    }

    [RelayCommand]
    private async Task DeleteExpenseLog(ExpenseLogVM? expenseLog)
    {
        if (expenseLog is null || expenseLog.IsForDeletion)
            return;

        await _expenseLogService.DeleteAsync(expenseLog.Id);

        ApplyDeletedExpenseLogToUi(expenseLog);

        if (expenseLog.Id > 0)
            Messenger.Send(new RecordLogMemoryMessage(new DeleteExpenseLogMemoryAction(expenseLog.Id)));
    }

    private void Initialize()
    {
        ConfigureExpenseViews();
        IsActive = true;
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

    private void LoadTags(IEnumerable<ExpenseTagVM> allTags)
    {
        Tags = new ObservableCollection<ExpenseTagVM>(allTags.Take(5));
        OtherTags = new ObservableCollection<ExpenseTagVM>(allTags.Skip(5));
        SynchronizeTagSelections(SelectedTag);
        OnPropertyChanged(nameof(HasOtherTags));
        OnPropertyChanged(nameof(IsSelectedTagInOtherTags));
    }

    private void ConfigureExpenseViews()
    {
        Needs = CollectionViewSource.GetDefaultView(_needsSource);
        Wants = CollectionViewSource.GetDefaultView(_wantsSource);
        Invest = CollectionViewSource.GetDefaultView(_investSource);

        Needs.Filter = FilterExpenseLog;
        Wants.Filter = FilterExpenseLog;
        Invest.Filter = FilterExpenseLog;
    }

    private void RefreshBudgetMetrics()
    {
        var totalIncomeAmount = _spendingSources.Sum(source => source.Balance);

        NeedsAvailable = decimal.Round(totalIncomeAmount * NeedsThreshold, 2);
        WantsAvailable = decimal.Round(totalIncomeAmount * WantsThreshold, 2);
        InvestAvailable = decimal.Round(totalIncomeAmount * InvestThreshold, 2);

        NeedsSpent = _allExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs)
            .Sum(log => log.Amount);
        WantsSpent = _allExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants)
            .Sum(log => log.Amount);
        InvestSpent = _allExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings)
            .Sum(log => log.Amount);
        TotalSpent = NeedsSpent + WantsSpent + InvestSpent;

        NeedsPercentage = CalculatePercentage(NeedsSpent, NeedsAvailable);
        WantsPercentage = CalculatePercentage(WantsSpent, WantsAvailable);
        InvestPercentage = CalculatePercentage(InvestSpent, InvestAvailable);
        DailyAllowance = CalculateDailyAllowance(totalIncomeAmount);
    }

    private int CalculateDailyAllowance(decimal totalIncomeAmount)
    {
        var daysLeft = Math.Max(
            1,
            DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month) - DateTime.Today.Day);

        return (int)((totalIncomeAmount * (1 - InvestThreshold) - TotalSpent) / daysLeft);
    }

    private static int CalculatePercentage(decimal spentAmount, decimal availableAmount)
    {
        if (availableAmount <= 0)
            return 0;

        return (int)Math.Round(spentAmount / availableAmount * 100, MidpointRounding.AwayFromZero);
    }

    private void ApplyVisibleExpenseLogs()
    {
        var visibleExpenseLogs = _selectedRange is { } range
            ? _allExpenseLogs.Where(log => log.DeductedOn.Date >= range.From.Date && log.DeductedOn.Date <= range.To.Date)
            : _allExpenseLogs;

        ReplaceExpenseLogs(
            _needsSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs));
        ReplaceExpenseLogs(
            _wantsSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants));
        ReplaceExpenseLogs(
            _investSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings));

        RefreshExpenseViews();
    }

    private bool FilterExpenseLog(object item)
    {
        if (item is not ExpenseLogVM expenseLog)
            return false;

        if (SelectedTag is not null &&
            expenseLog.Expense?.ExpenseTag?.Id != SelectedTag.Id)
            return false;

        return SelectedSpendingSourceId is null ||
               expenseLog.SpendingSource?.Id == SelectedSpendingSourceId;
    }

    private void SynchronizeTagSelections(ExpenseTagVM? selectedTag)
    {
        _isSynchronizingTagSelections = true;

        try
        {
            SelectedVisibleTag = selectedTag is null
                ? null
                : Enumerable.FirstOrDefault<ExpenseTagVM>(Tags, tag => tag.Id == selectedTag.Id);
            SelectedOtherTag = selectedTag is null
                ? null
                : Enumerable.FirstOrDefault<ExpenseTagVM>(OtherTags, tag => tag.Id == selectedTag.Id);
        }
        finally
        {
            _isSynchronizingTagSelections = false;
        }
    }

    private void RefreshExpenseViews()
    {
        Needs.Refresh();
        Wants.Refresh();
        Invest.Refresh();

        IsNeedsEmpty = Needs.IsEmpty;
        IsWantsEmpty = Wants.IsEmpty;
        IsInvestEmpty = Invest.IsEmpty;
    }

    private void SynchronizeSpendingSourceSelections(int? selectedSpendingSourceId)
    {
        foreach (var source in _spendingSources)
            source.IsSelected = selectedSpendingSourceId is int id && source.Id == id;
    }

    private void ApplyDeletedExpenseLogToUi(ExpenseLogVM expenseLog)
    {
        var trackedExpenseLog = _allExpenseLogs.FirstOrDefault(log => log.Id == expenseLog.Id) ?? expenseLog;
        trackedExpenseLog.IsForDeletion = true;

        _allExpenseLogs = _allExpenseLogs
            .Where(log => log.Id != trackedExpenseLog.Id)
            .ToList();

        RefreshBudgetMetrics();
        ApplyVisibleExpenseLogs();
    }

    private static void ReplaceExpenseLogs(ObservableCollection<ExpenseLogVM> target, IEnumerable<ExpenseLogVM> items)
    {
        target.Clear();

        foreach (var item in items.OrderByDescending(log => log.DeductedOn))
            target.Add(item);
    }
}
