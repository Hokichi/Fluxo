namespace Fluxo.ViewModels.Shell.StartupWizard;

public static class StartupWizardLoadingCoordinator
{
    public const int AttemptsPerCycle = 5;

    public static readonly TimeSpan MinimumLoadingDuration = TimeSpan.FromSeconds(5);

    public static async Task<StartupWizardLoadingOutcome> RunAsync(
        Func<Task<bool>> tryStageAsync,
        Func<Task<bool>> confirmRetryCycleAsync,
        Func<TimeSpan, Task> delayAsync)
    {
        ArgumentNullException.ThrowIfNull(tryStageAsync);
        ArgumentNullException.ThrowIfNull(confirmRetryCycleAsync);
        ArgumentNullException.ThrowIfNull(delayAsync);

        var minimumDelayTask = delayAsync(MinimumLoadingDuration);

        while (true)
        {
            for (var attempt = 0; attempt < AttemptsPerCycle; attempt++)
            {
                if (await tryStageAsync().ConfigureAwait(false))
                {
                    await minimumDelayTask.ConfigureAwait(false);
                    return StartupWizardLoadingOutcome.Success;
                }
            }

            if (!await confirmRetryCycleAsync().ConfigureAwait(false))
            {
                await minimumDelayTask.ConfigureAwait(false);
                return StartupWizardLoadingOutcome.Abandoned;
            }
        }
    }
}
