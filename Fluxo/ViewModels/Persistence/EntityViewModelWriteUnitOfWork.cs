using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Persistence;

public sealed class EntityViewModelWriteUnitOfWork(IUnitOfWork unitOfWork, AutoMapper.IMapper mapper)
    : IViewModelWriteUnitOfWork<ExpenseVM, ExpenseLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly AutoMapper.IMapper _mapper = mapper;
    private IWriteRepository<ExpenseVM>? _expenses;
    private IWriteRepository<ExpenseLogVM>? _expenseLogs;
    private IWriteRepository<ExpenseTagVM>? _expenseTags;
    private IWriteRepository<SavingGoalVM>? _savingGoals;
    private IWriteRepository<SpendingSourceVM>? _spendingSources;

    public IWriteRepository<ExpenseVM> Expenses => _expenses ??= new ViewModelWriteRepository<Expense, ExpenseVM>(_unitOfWork.Expenses, _mapper);
    public IWriteRepository<ExpenseLogVM> ExpenseLogs => _expenseLogs ??= new ViewModelWriteRepository<ExpenseLog, ExpenseLogVM>(_unitOfWork.ExpenseLogs, _mapper);
    public IWriteRepository<ExpenseTagVM> ExpenseTags => _expenseTags ??= new ViewModelWriteRepository<ExpenseTag, ExpenseTagVM>(_unitOfWork.ExpenseTags, _mapper);
    public IWriteRepository<SavingGoalVM> SavingGoals => _savingGoals ??= new ViewModelWriteRepository<SavingGoal, SavingGoalVM>(_unitOfWork.SavingGoals, _mapper);
    public IWriteRepository<SpendingSourceVM> SpendingSources => _spendingSources ??= new ViewModelWriteRepository<SpendingSource, SpendingSourceVM>(_unitOfWork.SpendingSources, _mapper);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _unitOfWork.DisposeAsync();
    }
}
