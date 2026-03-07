using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class TagRepository : BaseRepository<Tag>, ITagRepository
{
    public TagRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<int> GetUsageCountAsync(int tagId)
    {
        var expenseCount = await Db.ExpenseTags.CountAsync(et => et.TagId == tagId);
        var fixedCount = await Db.FixedExpenseTags.CountAsync(ft => ft.TagId == tagId);
        return expenseCount + fixedCount;
    }
}