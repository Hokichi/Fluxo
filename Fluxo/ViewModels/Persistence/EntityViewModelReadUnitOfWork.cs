using AutoMapper;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Persistence;

public sealed class EntityViewModelReadUnitOfWork(IUnitOfWork unitOfWork, FluxoDbContext dbContext, IMapper mapper)
    : IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>
{
    private readonly FluxoDbContext _dbContext = dbContext;
    private readonly IMapper _mapper = mapper;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private IExpenseLogReadRepository<ExpenseLogVM>? _expenseLogs;
    private IExpenseReadRepository<ExpenseVM>? _expenses;
    private IExpenseTagReadRepository<ExpenseTagVM>? _expenseTags;
    private IIncomeLogReadRepository<IncomeLogVM>? _incomeLogs;
    private IReadRepository<SavingGoalVM>? _savingGoals;
    private ISpendingSourceReadRepository<SpendingSourceVM>? _spendingSources;

    public IExpenseReadRepository<ExpenseVM> Expenses => _expenses ??=
        new ExpenseViewModelReadRepository<ExpenseVM>(_unitOfWork.Expenses, _dbContext, _mapper);

    public IExpenseLogReadRepository<ExpenseLogVM> ExpenseLogs => _expenseLogs ??=
        new ExpenseLogViewModelReadRepository<ExpenseLogVM>(_unitOfWork.ExpenseLogs, _dbContext, _mapper);

    public IIncomeLogReadRepository<IncomeLogVM> IncomeLogs => _incomeLogs ??=
        new IncomeLogViewModelReadRepository<IncomeLogVM>(_unitOfWork.IncomeLogs, _dbContext, _mapper);

    public IExpenseTagReadRepository<ExpenseTagVM> ExpenseTags => _expenseTags ??=
        new ExpenseTagViewModelReadRepository<ExpenseTagVM>(_unitOfWork.ExpenseTags, _dbContext, _mapper);

    public IReadRepository<SavingGoalVM> SavingGoals => _savingGoals ??=
        new ViewModelReadRepository<SavingGoal, SavingGoalVM>(_unitOfWork.SavingGoals, _mapper);

    public ISpendingSourceReadRepository<SpendingSourceVM> SpendingSources => _spendingSources ??=
        new SpendingSourceViewModelReadRepository<SpendingSourceVM>(_unitOfWork.SpendingSources, _dbContext, _mapper);

    public void Dispose()
    {
        _unitOfWork.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _unitOfWork.DisposeAsync();
    }
}