using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.ViewModels.Entities;

namespace Fluxo.Mappings;

public sealed class DtoViewModelProfile : Profile
{
    public DtoViewModelProfile()
    {
        CreateMap<ExpenseDto, ExpenseVM>().ReverseMap();
        CreateMap<ExpenseLogDto, ExpenseLogVM>()
            .ForMember(dest => dest.ParentLogId, opt => opt.MapFrom(src => src.ParentLogId))
            .ReverseMap()
            .ForMember(dest => dest.ParentLogId, opt => opt.MapFrom(src => src.ParentLogId));
        CreateMap<IncomeLogDto, IncomeLogVM>().ReverseMap();
        CreateMap<ExpenseTagDto, ExpenseTagVM>().ReverseMap();
        CreateMap<SavingGoalDto, SavingGoalVM>().ReverseMap();
        CreateMap<AccountDto, AccountVM>().ReverseMap();
        CreateMap<RecurringTransactionDto, RecurringTransactionVM>().ReverseMap();
        CreateMap<UserSettingsDto, UserSettingsVM>().ReverseMap();
    }
}
