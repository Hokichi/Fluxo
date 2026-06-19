using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowAppLockTests
{
    [Fact]
    public void HeaderIncludesAppLockButtonAndOverlay()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"HeaderAppLockButton\"", xaml);
        Assert.Contains("UncheckedText=\"Lock fluxo\"", xaml);
        Assert.Contains("CheckedText=\"Unlock fluxo\"", xaml);
        Assert.Contains("UncheckedIcon=\"{StaticResource Lock}\"", xaml);
        Assert.Contains("CheckedIcon=\"{StaticResource LockOpen}\"", xaml);
        Assert.Contains("x:Name=\"AppLockOverlay\"", xaml);
        Assert.Contains("Visibility=\"{Binding IsAppLocked", xaml);
    }

    [Fact]
    public void LockedStateConsumesHotkeysAndEnterPromptsUnlock()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (IsAppLocked())", source);
        Assert.Contains("if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)", source);
        Assert.Contains("UnlockAppUiFromUser();", source);
        Assert.Contains("MainWindowShortcutMatcher.IsToggleAppLockShortcut(e.Key, Keyboard.Modifiers)", source);
        Assert.Contains("LockAppUiFromUser();", source);
    }

    [Fact]
    public void AutoLockUsesActiveDelayThenCountdown()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("private static readonly TimeSpan AppAutoLockActiveDelay = TimeSpan.FromSeconds(10);", source);
        Assert.Contains("_appAutoLockActiveDelayTimer.Start();", source);
        Assert.Contains("StartAppAutoLockCountdown();", source);
        Assert.Contains("_appAutoLockCountdownTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, _mainVM.AppAutoLockedInterval));", source);
        Assert.Contains("return IsVisible && IsActive && WindowState != WindowState.Minimized;", source);
    }

    private static string ReadMainWindowXaml()
    {
        return File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml"));
    }

    private static string ReadMainWindowSource()
    {
        return File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));
    }
}
