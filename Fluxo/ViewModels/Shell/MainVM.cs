using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.ViewModels.Controls;

namespace Fluxo.ViewModels.Shell
{
    public partial class MainVM : ObservableRecipient
    {
        [ObservableProperty] private ObservableCollection<DayOfWeekVM> _daysOfWeek = new();
        [ObservableProperty] private DayOfWeekVM _selectedDay;

        public void Initialize()
        {
            var firstDayOfWeek = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek + 1);
            var daysThisWeek = Enumerable.Range(0, 7).Select(d => firstDayOfWeek.AddDays(d)).ToList();

            DaysOfWeek = new(daysThisWeek.Select((c => new DayOfWeekVM()
            { DayName = c.ToString("ddd"), DayNumber = c.Day.ToString(), IsSelected = c.Date == DateTime.Today })));
        }
    }
}