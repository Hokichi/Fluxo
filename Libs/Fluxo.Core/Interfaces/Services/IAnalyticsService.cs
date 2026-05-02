using Fluxo.Core.DTO;

namespace Fluxo.Core.Interfaces.Services;

public interface IAnalyticsService
{
    Task<AnalyticsDto> GetAnalyticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
