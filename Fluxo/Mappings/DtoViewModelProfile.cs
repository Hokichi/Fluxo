using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.ViewModels.Entities;

namespace Fluxo.Mappings;

public sealed class DtoViewModelProfile : Profile
{
    public DtoViewModelProfile()
    {
        CreateMap<ExpenseDto, ExpenseVM>().ReverseMap();
        CreateMap<ExpenseLogDto, ExpenseLogVM>().ReverseMap();
        CreateMap<IncomeLogDto, IncomeLogVM>().ReverseMap();
        CreateMap<ExpenseTagDto, ExpenseTagVM>().ReverseMap();
        CreateMap<SavingGoalDto, SavingGoalVM>().ReverseMap();
        CreateMap<AccountDto, AccountVM>().ReverseMap();
        CreateMap<RecurringTransactionDto, RecurringTransactionVM>().ReverseMap();
        CreateMap<UserSettingsDto, UserSettingsVM>().ReverseMap();
    }
}
