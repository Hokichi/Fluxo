using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using System.Windows.Data;

namespace Fluxo.ViewModels.Shell
{
    public partial class MainVM(IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM> readUnitOfWork) : ObservableRecipient
    {
        [ObservableProperty] private ObservableCollection<SpendingSourceVM> _spendingSources = new();

        [ObservableProperty] private ObservableCollection<ExpenseTagVM> _tags = new();
        [ObservableProperty] private ExpenseTagVM? _selectedTag;

        [ObservableProperty] private ObservableCollection<DayOfWeekVM> _daysOfWeek = new();
        [ObservableProperty] private DayOfWeekVM _selectedDay;

        [ObservableProperty] private bool _isNeedsEmpty;
        [ObservableProperty] private bool _isWantsEmpty;
        [ObservableProperty] private bool _isInvestEmpty;

        [ObservableProperty] private bool _hasNotifications;

        private readonly ObservableCollection<ExpenseLogVM> _needsSource = [];
        private readonly ObservableCollection<ExpenseLogVM> _wantsSource = [];
        private readonly ObservableCollection<ExpenseLogVM> _investSource = [];

        public ICollectionView Needs { get; private set; }
        public ICollectionView Wants { get; private set; }
        public ICollectionView Invest { get; private set; }

        partial void OnSelectedTagChanged(ExpenseTagVM? value)
        {
            RefreshExpenseViews();
        }

        [RelayCommand]
        private void ClearSelectedTag()
        {
            SelectedTag = null;
        }

        public async Task Initialize()
        {
            var firstDayOfWeek = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek + 1);
            var daysThisWeek = Enumerable.Range(0, 7).Select(d => firstDayOfWeek.AddDays(d)).ToList();

            DaysOfWeek = new(daysThisWeek.Select(c => new DayOfWeekVM
            {
                Date = c,
                DayName = c.ToString("ddd"),
                DayNumber = c.Day.ToString(),
                IsSelected = c.Date == DateTime.Today
            }));

            var spendingSources = await readUnitOfWork.SpendingSources.GetAllAsync();
            var trackedSourceTotals = await Task.WhenAll(
                spendingSources
                    .Where(source => source.SpendingSourceType is SpendingSourceType.Cash or SpendingSourceType.Checking)
                    .Select(async source => new
                    {
                        SourceName = source.Name,
                        MoneyIn = (await readUnitOfWork.IncomeLogs.GetBySpendingSourceIdAsync(source.Id)).Sum(log => log.Amount),
                        MoneyOut = (await readUnitOfWork.ExpenseLogs.GetBySpendingSourceIdAsync(source.Id)).Sum(log => log.Amount)
                    }));

            var totalsBySourceId = trackedSourceTotals.ToDictionary(
                total => total.SourceName,
                total => (total.MoneyIn, total.MoneyOut));

            SpendingSources = new(spendingSources.Select(source =>
            {
                if (source.SpendingSourceType is not (SpendingSourceType.Cash or SpendingSourceType.Checking))
                    return source;

                var totals = totalsBySourceId.GetValueOrDefault(source.Name);
                source.MoneyIn = totals.MoneyIn;
                source.MoneyOut = totals.MoneyOut;
                return source;
            }));

            ReplaceExpenseLogs(_needsSource, await readUnitOfWork.ExpenseLogs.GetByCategoryAsync(ExpenseCategory.Needs));
            ReplaceExpenseLogs(_wantsSource, await readUnitOfWork.ExpenseLogs.GetByCategoryAsync(ExpenseCategory.Wants));
            ReplaceExpenseLogs(_investSource, await readUnitOfWork.ExpenseLogs.GetByCategoryAsync(ExpenseCategory.Savings));

            Needs = CollectionViewSource.GetDefaultView(_needsSource);
            Wants = CollectionViewSource.GetDefaultView(_wantsSource);
            Invest = CollectionViewSource.GetDefaultView(_investSource);

            OnPropertyChanged(nameof(Needs));
            OnPropertyChanged(nameof(Wants));
            OnPropertyChanged(nameof(Invest));

            Needs.Filter = FilterBySelectedTag;
            Wants.Filter = FilterBySelectedTag;
            Invest.Filter = FilterBySelectedTag;

            Tags = new((await readUnitOfWork.ExpenseTags.GetTagsByCountDescendingAsync()).Select(c => c.Tag).Take(5));
            RefreshExpenseViews();
        }

        private bool FilterBySelectedTag(object item)
        {
            if (item is not ExpenseLogVM expenseLog)
            {
                return false;
            }

            if (SelectedTag is null)
            {
                return true;
            }

            return expenseLog.Expense?.ExpenseTag?.Id == SelectedTag.Id;
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

        private static void ReplaceExpenseLogs(ObservableCollection<ExpenseLogVM> target, IEnumerable<ExpenseLogVM> items)
        {
            target.Clear();

            foreach (var item in items.OrderByDescending(c => c.DeductedOn))
                target.Add(item);
        }
    }
}