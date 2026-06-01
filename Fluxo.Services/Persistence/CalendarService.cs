using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class CalendarService(IDataOperationRunner dataOperationRunner) : ICalendarService
{
    public Task<CalendarDto> GetCalendarDayAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        _ = dataOperationRunner;
        return Task.FromResult(new CalendarDto(date, 0m, 0m, [], [], [], []));
    }
}
