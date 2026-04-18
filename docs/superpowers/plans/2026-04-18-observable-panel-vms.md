# Observable Panel VMs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the snapshot pattern from all three panel ViewModels, make `SpendingSources` an `ObservableCollection`, and rewrite tests to use mocked services with `LoadAsync()`.

**Architecture:** Each panel VM's `LoadAsync()` fetches directly from services, maps to VMs, and updates observable collections in-place (Clear + re-add). The intermediate `*Snapshot` record types and `LoadSnapshot()` methods are deleted. Tests add NSubstitute to stub services and `IMapper`, then call `LoadAsync()` instead of `LoadSnapshot()`.

**Tech Stack:** C# 13, WPF, .NET 10, MVVM Community Toolkit, NSubstitute 5.x, xunit 2.9.x

---

## File Map

| Action | File |
|---|---|
| **Modify** | `Fluxo.Tests/Fluxo.Tests.csproj` |
| **Rewrite** | `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs` |
| **Rewrite** | `Fluxo.Tests/ViewModels/Shell/Main/BudgetAllocationPanelVMTests.cs` |
| **Modify** | `Fluxo/ViewModels/Shell/Main/NotificationPanelVM.cs` |
| **Rewrite** | `Fluxo.Tests/ViewModels/Shell/Main/NotificationPanelVMTests.cs` |
| **Modify** | `Fluxo/ViewModels/Shell/Main/SavingGoalsPanelVM.cs` |
| **Rewrite** | `Fluxo.Tests/ViewModels/Shell/Main/SavingGoalsPanelVMTests.cs` |
| **Modify** | `Fluxo/ViewModels/Shell/MainVM.cs` |

---

## Task 1: Add NSubstitute to the test project

- [ ] **Step 1: Add NSubstitute package**

```bash
dotnet add Fluxo.Tests/Fluxo.Tests.csproj package NSubstitute
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Fluxo.Tests/Fluxo.Tests.csproj --verbosity quiet
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo.Tests/Fluxo.Tests.csproj
git commit -m "chore: add NSubstitute to test project"
```

---

## Task 2: Refactor BudgetAllocationPanelVM

Remove `BudgetAllocationPanelSnapshot`, remove `LoadSnapshot()`, fold its logic into `LoadAsync()`, change `_spendingSources` to `ObservableCollection`, make services non-nullable, remove the parameterless constructor.

**Files:**
- Modify: `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs`
- Modify: `Fluxo/ViewModels/Shell/MainVM.cs`
- Rewrite: `Fluxo.Tests/ViewModels/Shell/Main/BudgetAllocationPanelVMTests.cs`

- [ ] **Step 1: Write the failing tests**

Replace the full contents of `Fluxo.Tests/ViewModels/Shell/Main/BudgetAllocationPanelVMTests.cs` with:

```csharp
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class BudgetAllocationPanelVMTests
{
    [Fact]
    public void DateRangeMessage_UpdatesDisplayedBucketsForIncludedDates()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();

            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 10),
                new DateTime(2026, 4, 12)));

            Assert.Collection(
                GetItems(vm.Needs),
                item => Assert.Equal(1, item.Id));
            Assert.Collection(
                GetItems(vm.Wants),
                item => Assert.Equal(2, item.Id));
            Assert.Empty(GetItems(vm.Invest));
        });
    }

    [Fact]
    public void SelectedTag_FiltersVisibleItemsAcrossBuckets()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();

            vm.SelectedVisibleTag = vm.Tags.Single(tag => tag.Id == 1);

            Assert.Equal(1, vm.SelectedTag?.Id);
            Assert.Collection(
                GetItems(vm.Needs),
                item => Assert.Equal(1, item.Id));
            Assert.Empty(GetItems(vm.Wants));
            Assert.Collection(
                GetItems(vm.Invest),
                item => Assert.Equal(3, item.Id));
        });
    }

    private static BudgetAllocationPanelVM CreateVm(
        IMessenger messenger,
        IReadOnlyList<ExpenseLogVM> expenseLogs,
        IReadOnlyList<ExpenseTagVM> tags,
        IReadOnlyList<SpendingSourceVM> spendingSources)
    {
        var expenseLogService = Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLogDto>>([]));

        var spendingSourceService = Substitute.For<ISpendingSourceService>();
        spendingSourceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpendingSourceDto>>([]));

        var tagService = Substitute.For<ITagService>();
        tagService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseTagDto>>([]));

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns(expenseLogs);
        mapper.Map<IReadOnlyList<SpendingSourceVM>>(Arg.Any<object>()).Returns(spendingSources);
        mapper.Map<IReadOnlyList<ExpenseTagVM>>(Arg.Any<object>()).Returns(tags);

        return new BudgetAllocationPanelVM(
            expenseLogService, spendingSourceService, tagService, mapper, messenger);
    }

    private static IReadOnlyList<ExpenseLogVM> CreateExpenseLogs()
    {
        var groceries = new ExpenseTagVM { Id = 1, Name = "Groceries", HexCode = "#22C55E" };
        var fun = new ExpenseTagVM { Id = 2, Name = "Fun", HexCode = "#F97316" };
        var source = new SpendingSourceVM
        {
            Id = 1,
            Name = "Checking",
            SpendingSourceType = SpendingSourceType.Checking,
            Balance = 2000m,
            IsEnabled = true,
            ShowOnUI = true
        };

        return
        [
            new ExpenseLogVM
            {
                Id = 1,
                Amount = 45m,
                DeductedOn = new DateTime(2026, 4, 10),
                Expense = new ExpenseVM
                {
                    Id = 11,
                    Name = "Groceries",
                    ExpenseCategory = ExpenseCategory.Needs,
                    ExpenseTag = groceries
                },
                SpendingSource = source
            },
            new ExpenseLogVM
            {
                Id = 2,
                Amount = 30m,
                DeductedOn = new DateTime(2026, 4, 12),
                Expense = new ExpenseVM
                {
                    Id = 12,
                    Name = "Movie",
                    ExpenseCategory = ExpenseCategory.Wants,
                    ExpenseTag = fun
                },
                SpendingSource = source
            },
            new ExpenseLogVM
            {
                Id = 3,
                Amount = 100m,
                DeductedOn = new DateTime(2026, 4, 18),
                Expense = new ExpenseVM
                {
                    Id = 13,
                    Name = "ETF",
                    ExpenseCategory = ExpenseCategory.Savings,
                    ExpenseTag = groceries
                },
                SpendingSource = source
            }
        ];
    }

    private static IReadOnlyList<ExpenseTagVM> CreateTags()
    {
        return
        [
            new ExpenseTagVM { Id = 1, Name = "Groceries", HexCode = "#22C55E" },
            new ExpenseTagVM { Id = 2, Name = "Fun", HexCode = "#F97316" }
        ];
    }

    private static IReadOnlyList<SpendingSourceVM> CreateSpendingSources()
    {
        return
        [
            new SpendingSourceVM
            {
                Id = 1,
                Name = "Checking",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 2000m,
                IsEnabled = true,
                ShowOnUI = true
            }
        ];
    }

    private static List<ExpenseLogVM> GetItems(ICollectionView view)
    {
        return view.Cast<ExpenseLogVM>().ToList();
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "BudgetAllocationPanelVMTests" --verbosity normal
```

Expected: FAIL — `BudgetAllocationPanelVM` still has `LoadSnapshot()` and a parameterless constructor; the `CreateVm` helper references the full constructor that currently has nullable services.

- [ ] **Step 3: Replace BudgetAllocationPanelVM.cs**

Replace the full contents of `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs` with:

```csharp
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

namespace Fluxo.ViewModels.Shell;

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

        Needs.Filter = FilterBySelectedTag;
        Wants.Filter = FilterBySelectedTag;
        Invest.Filter = FilterBySelectedTag;
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

    private bool FilterBySelectedTag(object item)
    {
        if (item is not ExpenseLogVM expenseLog)
            return false;

        if (SelectedTag is null)
            return true;

        return expenseLog.Expense?.ExpenseTag?.Id == SelectedTag.Id;
    }

    private void SynchronizeTagSelections(ExpenseTagVM? selectedTag)
    {
        _isSynchronizingTagSelections = true;

        try
        {
            SelectedVisibleTag = selectedTag is null
                ? null
                : Tags.FirstOrDefault(tag => tag.Id == selectedTag.Id);
            SelectedOtherTag = selectedTag is null
                ? null
                : OtherTags.FirstOrDefault(tag => tag.Id == selectedTag.Id);
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
```

- [ ] **Step 4: Update MainVM.SpendingSources return type**

In `Fluxo/ViewModels/Shell/MainVM.cs`, change the `SpendingSources` property:

```csharp
public ObservableCollection<SpendingSourceVM> SpendingSources => BudgetPanel.SpendingSources;
```

Also add the missing using at the top:

```csharp
using System.Collections.ObjectModel;
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "BudgetAllocationPanelVMTests" --verbosity normal
```

Expected: 2 tests pass.

- [ ] **Step 6: Full build check**

```bash
dotnet build Fluxo/Fluxo.csproj --verbosity quiet 2>&1 | grep "CS[0-9]"
```

Expected: no output (no C# compiler errors).

- [ ] **Step 7: Commit**

```bash
git add Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs Fluxo/ViewModels/Shell/MainVM.cs Fluxo.Tests/ViewModels/Shell/Main/BudgetAllocationPanelVMTests.cs
git commit -m "refactor: remove BudgetAllocationPanelSnapshot, make SpendingSources ObservableCollection"
```

---

## Task 3: Refactor NotificationPanelVM

Remove `NotificationPanelSnapshot`, remove `LoadSnapshot()`, fold its logic into `LoadAsync()`, make services non-nullable, remove parameterless constructor.

**Files:**
- Modify: `Fluxo/ViewModels/Shell/Main/NotificationPanelVM.cs`
- Rewrite: `Fluxo.Tests/ViewModels/Shell/Main/NotificationPanelVMTests.cs`

- [ ] **Step 1: Write the failing test**

Replace the full contents of `Fluxo.Tests/ViewModels/Shell/Main/NotificationPanelVMTests.cs` with:

```csharp
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class NotificationPanelVMTests
{
    [Fact]
    public async Task LoadAsync_WhenCalledTwice_DoesNotDuplicateSystemNotifications()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var spendingSources = new List<SpendingSourceVM>
        {
            new()
            {
                Id = 1,
                Name = "Visa",
                SpendingSourceType = SpendingSourceType.Credit,
                MonthlyDueDate = dueDate.Day,
                AccountLimit = 1000m,
                SpentAmount = 250m
            }
        };

        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: spendingSources);

        await vm.LoadAsync();
        await vm.LoadAsync();

        Assert.Equal(1, vm.NotificationCount);
        Assert.Equal(1, vm.Notifications.Select(n => n.Key).Distinct().Count());
    }

    private static NotificationPanelVM CreateVm(
        IReadOnlyList<ExpenseVM> expenses,
        IReadOnlyList<ExpenseLogVM> expenseLogs,
        IReadOnlyList<SpendingSourceVM> spendingSources)
    {
        var expenseService = Substitute.For<IExpenseService>();
        expenseService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseDto>>([]));

        var expenseLogService = Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLogDto>>([]));

        var spendingSourceService = Substitute.For<ISpendingSourceService>();
        spendingSourceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpendingSourceDto>>([]));

        var userSettingsRepository = Substitute.For<IUserSettingsRepository>();
        userSettingsRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>([]));

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<ExpenseVM>>(Arg.Any<object>()).Returns(expenses);
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns(expenseLogs);
        mapper.Map<IReadOnlyList<SpendingSourceVM>>(Arg.Any<object>()).Returns(spendingSources);

        return new NotificationPanelVM(
            expenseService,
            expenseLogService,
            spendingSourceService,
            userSettingsRepository,
            mapper,
            new WeakReferenceMessenger());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "NotificationPanelVMTests" --verbosity normal
```

Expected: FAIL — `NotificationPanelVM` still requires the parameterless constructor path used by the test (it currently uses `new NotificationPanelVM()`, but after this refactor the test uses the full constructor which is fine).

- [ ] **Step 4: Remove NotificationPanelSnapshot and LoadSnapshot, update LoadAsync**

In `Fluxo/ViewModels/Shell/Main/NotificationPanelVM.cs`:

1. Delete lines 19–22 (the `NotificationPanelSnapshot` record):
```csharp
public sealed record NotificationPanelSnapshot(
    IReadOnlyList<ExpenseVM> Expenses,
    IReadOnlyList<ExpenseLogVM> ExpenseLogs,
    IReadOnlyList<SpendingSourceVM> SpendingSources);
```

2. Change private service fields from nullable to non-nullable (remove `?`):
```csharp
private readonly IExpenseLogService _expenseLogService;
private readonly IExpenseService _expenseService;
private readonly IMapper _mapper;
private readonly ISpendingSourceService _spendingSourceService;
private readonly IUserSettingsRepository _userSettingsRepository;
```

3. Remove the parameterless constructor (lines 73–78):
```csharp
public NotificationPanelVM(IMessenger? messenger = null)
    : base(messenger ?? WeakReferenceMessenger.Default)
{
    Notifications.CollectionChanged += OnNotificationsCollectionChanged;
    IsActive = true;
}
```

4. In the full constructor, remove the nullable assignments and keep:
```csharp
public NotificationPanelVM(
    IExpenseService expenseService,
    IExpenseLogService expenseLogService,
    ISpendingSourceService spendingSourceService,
    IUserSettingsRepository userSettingsRepository,
    IMapper mapper,
    IMessenger? messenger = null)
    : base(messenger ?? WeakReferenceMessenger.Default)
{
    _expenseService = expenseService;
    _expenseLogService = expenseLogService;
    _spendingSourceService = spendingSourceService;
    _userSettingsRepository = userSettingsRepository;
    _mapper = mapper;

    Notifications.CollectionChanged += OnNotificationsCollectionChanged;
    IsActive = true;
}
```

5. Replace `LoadAsync()` with:
```csharp
public async Task LoadAsync(CancellationToken cancellationToken = default)
{
    await LoadUserSettingsAsync(cancellationToken);

    _expenses = _mapper.Map<IReadOnlyList<ExpenseVM>>(
        await _expenseService.GetAllAsync(cancellationToken));
    _expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
        await _expenseLogService.GetAllAsync(cancellationToken))
        .Where(log => !log.IsForDeletion).ToList();
    _spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
        await _spendingSourceService.GetAllAsync(cancellationToken));

    RefreshNotifications();
}
```

6. Delete the `LoadSnapshot(NotificationPanelSnapshot snapshot)` method entirely.

7. Replace `ReloadSnapshotFromServicesAsync()` with `ReloadFromServicesAsync()`:
```csharp
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
```

8. Update `Receive(DashboardDataInvalidatedMessage message)` to call `ReloadFromServicesAsync()`:
```csharp
public void Receive(DashboardDataInvalidatedMessage message)
{
    if (!message.Value.HasFlag(DashboardDataInvalidationScope.Notifications))
        return;

    _ = ReloadFromServicesAsync();
}
```

9. Replace `LoadUserSettingsAsync` — remove the null guard at the top:
```csharp
private async Task LoadUserSettingsAsync(CancellationToken cancellationToken)
{
    var settings = await _userSettingsRepository.GetAllAsync(cancellationToken);
    // rest of method unchanged
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "NotificationPanelVMTests" --verbosity normal
```

Expected: 1 test passes.

- [ ] **Step 6: Commit**

```bash
git add Fluxo/ViewModels/Shell/Main/NotificationPanelVM.cs Fluxo.Tests/ViewModels/Shell/Main/NotificationPanelVMTests.cs
git commit -m "refactor: remove NotificationPanelSnapshot, fold LoadSnapshot into LoadAsync"
```

---

## Task 4: Refactor SavingGoalsPanelVM

Remove `LoadSnapshot(IEnumerable<SavingGoalVM>)`, fold its filtering logic into `LoadAsync()`, make fields non-nullable, remove parameterless constructor.

**Files:**
- Modify: `Fluxo/ViewModels/Shell/Main/SavingGoalsPanelVM.cs`
- Rewrite: `Fluxo.Tests/ViewModels/Shell/Main/SavingGoalsPanelVMTests.cs`

- [ ] **Step 1: Write the failing tests**

Replace the full contents of `Fluxo.Tests/ViewModels/Shell/Main/SavingGoalsPanelVMTests.cs` with:

```csharp
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class SavingGoalsPanelVMTests
{
    [Fact]
    public async Task LoadAsync_FiltersCompletedGoals()
    {
        var goals = new List<SavingGoalVM>
        {
            new() { Id = 1, Name = "Emergency Fund", TargetAmount = 1000m, CurrentAmount = 250m },
            new() { Id = 2, Name = "Laptop",         TargetAmount = 1500m, CurrentAmount = 1500m }
        };

        var vm = CreateVm(goals);
        await vm.LoadAsync();

        Assert.True(vm.HasSavingGoals);
        var remainingGoal = Assert.Single(vm.SavingGoals);
        Assert.Equal(1, remainingGoal.Id);
        Assert.Equal(0, vm.CurrentGoalIndex);
        Assert.Equal(1, vm.CurrentGoal?.Id);
        var activeDot = Assert.Single(vm.GoalDots, dot => dot.IsActive);
        Assert.Equal(vm.GoalDots[0], activeDot);
    }

    [Fact]
    public async Task NavigatePrevious_WrapsFromFirstToLastGoal()
    {
        var vm = CreateVm(CreateGoals(3));
        await vm.LoadAsync();

        vm.NavigatePrevious();

        Assert.Equal(2, vm.CurrentGoalIndex);
        Assert.Equal(3, vm.CurrentGoal?.Id);
        Assert.Equal(1, vm.NavigationDirection);
        Assert.Equal(3, vm.GoalDots.Count);
        Assert.True(vm.GoalDots[2].IsActive);
    }

    [Fact]
    public async Task NavigateNext_WrapsFromLastToFirstGoal()
    {
        var vm = CreateVm(CreateGoals(2));
        await vm.LoadAsync();

        vm.NavigatePrevious();
        vm.NavigateNext();

        Assert.Equal(0, vm.CurrentGoalIndex);
        Assert.Equal(1, vm.CurrentGoal?.Id);
        Assert.Equal(-1, vm.NavigationDirection);
        Assert.True(vm.GoalDots[0].IsActive);
        Assert.False(vm.GoalDots[1].IsActive);
    }

    private static SavingGoalsPanelVM CreateVm(IReadOnlyList<SavingGoalVM> goals)
    {
        var savingGoalRepository = Substitute.For<ISavingGoalRepository>();
        savingGoalRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SavingGoal>>([]));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SavingGoals.Returns(savingGoalRepository);

        var userSettingsRepository = Substitute.For<IUserSettingsRepository>();
        userSettingsRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>([]));

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<SavingGoalDto>>(Arg.Any<object>()).Returns(new List<SavingGoalDto>());
        mapper.Map<IReadOnlyList<SavingGoalVM>>(Arg.Any<object>()).Returns(goals);

        return new SavingGoalsPanelVM(
            unitOfWork,
            mapper,
            userSettingsRepository,
            new WeakReferenceMessenger());
    }

    private static IReadOnlyList<SavingGoalVM> CreateGoals(int count)
    {
        return Enumerable.Range(1, count)
            .Select(id => new SavingGoalVM
            {
                Id = id,
                Name = $"Goal {id}",
                TargetAmount = 1000m,
                CurrentAmount = 100m
            })
            .ToList();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "SavingGoalsPanelVMTests" --verbosity normal
```

Expected: FAIL — `SavingGoalsPanelVM` still has `LoadSnapshot()` and a parameterless constructor.

- [ ] **Step 4: Update SavingGoalsPanelVM.cs**

In `Fluxo/ViewModels/Shell/Main/SavingGoalsPanelVM.cs`:

1. Change private fields from nullable to non-nullable:
```csharp
private readonly IMapper _mapper;
private readonly IUnitOfWork _unitOfWork;
private readonly IUserSettingsRepository _userSettingsRepository;
```

2. Remove the parameterless constructor (lines 38–42):
```csharp
public SavingGoalsPanelVM(IMessenger? messenger = null)
    : base(messenger ?? WeakReferenceMessenger.Default)
{
    IsActive = true;
}
```

3. Replace `LoadAsync()` with (folds in the former `LoadSnapshot` logic):
```csharp
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
```

4. Delete the `LoadSnapshot(IEnumerable<SavingGoalVM> savingGoals)` method entirely.

5. Replace `ReloadSnapshotFromServicesAsync()` with `ReloadFromServicesAsync()` and remove the null guard:
```csharp
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
```

6. Update `Receive(DashboardDataInvalidatedMessage message)` to call `ReloadFromServicesAsync()`:
```csharp
public void Receive(DashboardDataInvalidatedMessage message)
{
    if (!message.Value.HasFlag(DashboardDataInvalidationScope.SavingGoals))
        return;

    _ = ReloadFromServicesAsync();
}
```

7. Remove null guard from `LoadSavingGoalSettingsAsync`:
```csharp
private async Task LoadSavingGoalSettingsAsync(CancellationToken cancellationToken)
{
    var settings = await _userSettingsRepository.GetAllAsync(cancellationToken);
    // rest of method unchanged
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "SavingGoalsPanelVMTests" --verbosity normal
```

Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```bash
git add Fluxo/ViewModels/Shell/Main/SavingGoalsPanelVM.cs Fluxo.Tests/ViewModels/Shell/Main/SavingGoalsPanelVMTests.cs
git commit -m "refactor: remove SavingGoalsPanelVM.LoadSnapshot, fold logic into LoadAsync"
```

---

## Task 5: Verify and finalize

- [ ] **Step 1: Run the full test suite**

```bash
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --verbosity normal
```

Expected: all 41+ tests pass (the 3 rewritten test classes plus all pre-existing tests).

- [ ] **Step 2: Full solution build**

```bash
dotnet build Fluxo.slnx --verbosity quiet 2>&1 | tail -5
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Confirm no snapshot types remain**

```bash
grep -r "Snapshot" Fluxo/ViewModels/ --include="*.cs"
```

Expected: no output (all `*Snapshot` records and `LoadSnapshot` methods removed).

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "refactor: complete observable panel VMs — snapshot pattern fully removed"
```

---

## Verification Checklist

After all tasks:

- [ ] `BudgetAllocationPanelSnapshot` record deleted
- [ ] `NotificationPanelSnapshot` record deleted
- [ ] No `LoadSnapshot()` method on any panel VM
- [ ] `BudgetAllocationPanelVM._spendingSources` is `ObservableCollection<SpendingSourceVM>`
- [ ] `BudgetAllocationPanelVM.SpendingSources` returns `ObservableCollection<SpendingSourceVM>`
- [ ] `MainVM.SpendingSources` returns `ObservableCollection<SpendingSourceVM>`
- [ ] No nullable service fields on any panel VM
- [ ] No null guards in `LoadAsync()` / `LoadUserSettingsAsync()` / `LoadSavingGoalSettingsAsync()`
- [ ] No parameterless constructors on panel VMs
- [ ] All tests pass
- [ ] Build clean
