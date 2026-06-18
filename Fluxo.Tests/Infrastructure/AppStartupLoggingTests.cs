using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class AppStartupLoggingTests
{
    [Fact]
    public void AppStartup_LogsBootStagesBeforeAndAfterCriticalStartupWork()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "App.xaml.cs"));

        Assert.Contains("LogStartupStage(\"single-instance check\", StartupStageState.Started);", source);
        Assert.Contains("LogStartupStage(\"database directory\", StartupStageState.Started);", source);
        Assert.Contains("LogStartupStage(\"database migration\", StartupStageState.Completed);", source);
        Assert.Contains("LogStartupStage(\"startup loader\", StartupStageState.Started);", source);
        Assert.Contains("LogStartupStage(\"main window\", StartupStageState.Completed);", source);
        Assert.Contains("LogStartupStage(\"startup failure\", StartupStageState.Failed);", source);
    }

    [Fact]
    public void AppConstructor_InitializesBootstrapLoggingBeforeServiceProviderConstruction()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "App.xaml.cs"));

        var constructorIndex = source.IndexOf("public App()", StringComparison.Ordinal);
        Assert.True(constructorIndex >= 0);

        var bootstrapIndex = source.IndexOf("InitializeBootstrapLogging();", constructorIndex, StringComparison.Ordinal);
        var serviceCollectionIndex = source.IndexOf("var services = new ServiceCollection();", constructorIndex, StringComparison.Ordinal);

        Assert.True(bootstrapIndex >= 0);
        Assert.True(serviceCollectionIndex >= 0);
        Assert.True(bootstrapIndex < serviceCollectionIndex);
    }

    [Fact]
    public void AppStartup_CatchesDatabaseStartupFailuresForPopupAndIssueLog()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "App.xaml.cs"));
        var onStartup = ExtractMethodBodyBySignature(source, "protected override async void OnStartup(StartupEventArgs e)");

        var tryIndex = onStartup.IndexOf("try", StringComparison.Ordinal);
        var migrationIndex = onStartup.IndexOf("await MigrateDatabaseAsync(_dataOperationRunner);", StringComparison.Ordinal);
        var catchIndex = onStartup.IndexOf("catch (Exception exception)", StringComparison.Ordinal);
        var popupIndex = onStartup.IndexOf("dialogService.ShowError", StringComparison.Ordinal);

        Assert.True(tryIndex >= 0);
        Assert.True(migrationIndex >= 0);
        Assert.True(catchIndex >= 0);
        Assert.True(popupIndex >= 0);
        Assert.True(tryIndex < migrationIndex);
        Assert.True(migrationIndex < catchIndex);
        Assert.True(catchIndex < popupIndex);
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found.");

        var openingBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openingBraceIndex >= 0, $"Opening brace for method signature '{signatureMarker}' was not found.");

        var depth = 0;
        for (var index = openingBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
                continue;
            }

            if (source[index] != '}')
                continue;

            depth--;
            if (depth == 0)
                return source.Substring(openingBraceIndex + 1, index - openingBraceIndex - 1);
        }

        throw new InvalidOperationException($"Closing brace for method signature '{signatureMarker}' was not found.");
    }
}
