using Fluxo.Core.Interfaces;
using Fluxo.Data.Extensions;
using Fluxo.Extensions;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Fluxo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IServiceProvider? _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            services
                .AddFluxoData()
                .AddFluxoPresentation()
                .AddUIData();

            _serviceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create and show the main window using the DI container
            var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        internal async Task DeleteMarkedExpenseLogsAsync(MainVM mainViewModel, CancellationToken cancellationToken = default)
        {
            if (_serviceProvider is null)
                return;

            var expenseLogIdsMarkedForDeletion = mainViewModel
                .GetExpenseLogIdsMarkedForDeletion()
                .ToHashSet();

            await using var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

            var expenseLogs = await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken);
            var expenseLogsToDelete = expenseLogs
                .Where(log => log.IsForDeletion || expenseLogIdsMarkedForDeletion.Contains(log.Id))
                .ToList();

            if (expenseLogsToDelete.Count == 0)
                return;

            var expenseIdsToDelete = expenseLogsToDelete
                .Select(log => log.Expense?.Id ?? 0)
                .Where(id => id > 0)
                .ToHashSet();

            foreach (var expenseLog in expenseLogsToDelete)
            {
                unitOfWork.ExpenseLogs.Remove(expenseLog);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (expenseIdsToDelete.Count == 0)
                return;

            var remainingExpenseLogExpenseIds = (await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken))
                .Select(log => log.Expense?.Id ?? 0)
                .Where(id => id > 0)
                .ToHashSet();

            expenseIdsToDelete.ExceptWith(remainingExpenseLogExpenseIds);
            if (expenseIdsToDelete.Count == 0)
                return;

            var expenses = await unitOfWork.Expenses.GetAllAsync(cancellationToken);
            foreach (var expense in expenses.Where(expense => expenseIdsToDelete.Contains(expense.Id)))
            {
                unitOfWork.Expenses.Remove(expense);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
