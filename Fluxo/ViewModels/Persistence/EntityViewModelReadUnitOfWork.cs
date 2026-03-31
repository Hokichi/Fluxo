using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Persistence;

public sealed class EntityViewModelReadUnitOfWork(IUnitOfWork unitOfWork, AutoMapper.IMapper mapper)
    : IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly AutoMapper.IMapper _mapper = mapper;
    private IReadRepository<ExpenseVM>? _expenses;
    private IReadRepository<ExpenseLogVM>? _expenseLogs;
    private IReadRepository<ExpenseTagVM>? _expenseTags;
    private IReadRepository<SavingGoalVM>? _savingGoals;
    private IReadRepository<SpendingSourceVM>? _spendingSources;

    public IReadRepository<ExpenseVM> Expenses => _expenses ??= new ViewModelReadRepository<Expense, ExpenseVM>(_unitOfWork.Expenses, _mapper);
    public IReadRepository<ExpenseLogVM> ExpenseLogs => _expenseLogs ??= new ViewModelReadRepository<ExpenseLog, ExpenseLogVM>(_unitOfWork.ExpenseLogs, _mapper);
    public IReadRepository<ExpenseTagVM> ExpenseTags => _expenseTags ??= new ViewModelReadRepository<ExpenseTag, ExpenseTagVM>(_unitOfWork.ExpenseTags, _mapper);
    public IReadRepository<SavingGoalVM> SavingGoals => _savingGoals ??= new ViewModelReadRepository<SavingGoal, SavingGoalVM>(_unitOfWork.SavingGoals, _mapper);
    public IReadRepository<SpendingSourceVM> SpendingSources => _spendingSources ??= new ViewModelReadRepository<SpendingSource, SpendingSourceVM>(_unitOfWork.SpendingSources, _mapper);

    public void Dispose()
    {
        _unitOfWork.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _unitOfWork.DisposeAsync();
    }
}
