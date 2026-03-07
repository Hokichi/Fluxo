using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IFixedExpenseRepository : IRepository<FixedExpense>
{
    Task<IReadOnlyList<FixedExpense>> GetAllActiveAsync();

    /// <summary>
    /// Fixed expenses whose DueDay falls in the current month and haven't been
    /// paid yet this cycle (LastPaidDate is null or from a previous month).
    /// </summary>
    Task<IReadOnlyList<FixedExpense>> GetUnpaidForMonthAsync(int month, int year);

    /// <summary>
    /// Fixed expenses due within the next <paramref name="daysAhead"/> days.
    /// Used by NotificationService on app startup.
    /// </summary>
    Task<IReadOnlyList<FixedExpense>> GetDueSoonAsync(int daysAhead);

    /// <summary>Marks LastPaidDate; does NOT write a history row (that's the service's job).</summary>
    Task MarkAsPaidAsync(int id, DateTime paidDate);

    Task DeactivateAsync(int id);
}