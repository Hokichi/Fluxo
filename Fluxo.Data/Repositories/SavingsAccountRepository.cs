using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class SavingsAccountRepository : BaseRepository<SavingsAccount>, ISavingsAccountRepository
{
    public SavingsAccountRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<SavingsAccount>> GetAllActiveAsync()
        => await Db.SavingsAccounts
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync();

    public async Task UpdateBalanceAsync(int id, decimal newBalance)
    {
        var account = await Db.SavingsAccounts.FindAsync(id)
                      ?? throw new InvalidOperationException($"SavingsAccount {id} not found.");
        account.CurrentBalance = newBalance;
        account.UpdatedAt = DateTime.UtcNow;
        Db.SavingsAccounts.Update(account);
    }

    public async Task DeactivateAsync(int id)
    {
        var account = await Db.SavingsAccounts.FindAsync(id);
        if (account is null) return;
        account.IsActive = false;
        Db.SavingsAccounts.Update(account);
    }
}