using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.ViewModels.Shell
{
    public partial class MainVM(IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM> readUnitOfWork) : ObservableRecipient
    {
        [ObservableProperty] private ObservableCollection<DayOfWeekVM> _daysOfWeek = new();
        [ObservableProperty] private ObservableCollection<SpendingSourceVM> _spendingSources = new();
        [ObservableProperty] private ObservableCollection<ExpenseLogVM> _needs = new();
        [ObservableProperty] private ObservableCollection<ExpenseLogVM> _wants = new();
        [ObservableProperty] private ObservableCollection<ExpenseLogVM> _invest = new();
        [ObservableProperty] private DayOfWeekVM _selectedDay;

        [ObservableProperty] private bool _hasNotifications;

        public async Task Initialize()
        {
            var firstDayOfWeek = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek + 1);
            var daysThisWeek = Enumerable.Range(0, 7).Select(d => firstDayOfWeek.AddDays(d)).ToList();

            DaysOfWeek = new(daysThisWeek.Select(c => new DayOfWeekVM
            {
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

            Needs = new((await readUnitOfWork.ExpenseLogs.GetByCategoryAsync(ExpenseCategory.Needs)).OrderByDescending(c => c.DeductedOn));
            Wants = new((await readUnitOfWork.ExpenseLogs.GetByCategoryAsync(ExpenseCategory.Wants)).OrderByDescending(c => c.DeductedOn));
            Invest = new((await readUnitOfWork.ExpenseLogs.GetByCategoryAsync(ExpenseCategory.Savings)).OrderByDescending(c => c.DeductedOn));
        }
    }
}