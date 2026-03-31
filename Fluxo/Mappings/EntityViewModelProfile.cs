using AutoMapper;
using Fluxo.Core.Entities;
using Fluxo.ViewModels.Entities;

namespace Fluxo.Mappings;

public sealed class EntityViewModelProfile : Profile
{
    public EntityViewModelProfile()
    {
        CreateMap<Expense, ExpenseVM>().ReverseMap();
        CreateMap<ExpenseLog, ExpenseLogVM>().ReverseMap();
        CreateMap<ExpenseTag, ExpenseTagVM>().ReverseMap();
        CreateMap<SavingGoal, SavingGoalVM>().ReverseMap();
        CreateMap<SpendingSource, SpendingSourceVM>().ReverseMap();
    }
}
