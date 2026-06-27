using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;

namespace Fluxo.Services.Mappings;

public sealed class EntityDtoProfile : Profile
{
    public EntityDtoProfile()
    {
        CreateMap<Expense, ExpenseDto>().ReverseMap();
        CreateMap<ExpenseLog, ExpenseLogDto>()
            .ForMember(dest => dest.ParentLogId, opt => opt.MapFrom(src => src.ParentLogId))
            .ReverseMap()
            .ForMember(dest => dest.ParentLogId, opt => opt.MapFrom(src => src.ParentLogId));
        CreateMap<Tag, TagDto>().ReverseMap();
        CreateMap<IncomeLog, IncomeLogDto>().ReverseMap();
        CreateMap<SavingGoal, SavingGoalDto>().ReverseMap();
        CreateMap<RecurringTransaction, RecurringTransactionDto>().ReverseMap();
        CreateMap<UserSettings, UserSettingsDto>().ReverseMap();

        // Account: ignore computed fields when mapping Entity→DTO,
        // and ignore Id when mapping DTO→Entity (EF assigns Id on insert).
        CreateMap<Account, AccountDto>()
            .ForMember(dest => dest.MoneyIn, opt => opt.Ignore())
            .ForMember(dest => dest.MoneyOut, opt => opt.Ignore())
            .ReverseMap()
            .ForMember(dest => dest.Id, opt => opt.Ignore());
    }
}
