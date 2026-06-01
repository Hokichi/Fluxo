using Fluxo.Core.DTO;

namespace Fluxo.Core.Interfaces.Services;

public interface ICalendarService
{
    Task<CalendarDto> GetCalendarDayAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);
}
