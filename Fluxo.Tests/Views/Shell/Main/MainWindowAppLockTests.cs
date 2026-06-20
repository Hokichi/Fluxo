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
        Assert.Contains("Click=\"OnAppLockOverlayClick\"", xaml);
        Assert.Contains("Text=\"flux\"", xaml);
        Assert.Contains("Text=\"o\"", xaml);
        Assert.Contains("Text=\" is locked\"", xaml);
        Assert.Contains("FontFamily=\"{StaticResource Black}\"", xaml);
        Assert.Contains("Foreground=\"{StaticResource Brush.Mint}\"", xaml);
    }

    [Fact]
    public void LockedStateDisablesHeaderActionsButKeepsLockButtonAvailable()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Key=\"HeaderButtonDisableWhenAppLockedStyle\"", xaml);
        Assert.Contains("x:Key=\"HeaderButtonDisableWhenAppLockedAndSufficientFundsActionGateLockedStyle\"", xaml);
        Assert.Contains("x:Key=\"HeaderSearchRegionLockAndGateStyle\"", xaml);
        Assert.Contains("x:Name=\"HeaderMenuButton\"", xaml);
        Assert.Contains("x:Name=\"HeaderNotificationButton\"", xaml);
        Assert.Contains("x:Name=\"HeaderSearchRegion\"", xaml);
        Assert.Contains("x:Name=\"HeaderQuickAddButton\"", xaml);
        Assert.Contains("Style=\"{StaticResource HeaderButtonDisableWhenAppLockedStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource HeaderButtonDisableWhenAppLockedAndSufficientFundsActionGateLockedStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource HeaderSearchRegionLockAndGateStyle}\"", xaml);
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
        Assert.Contains("SetDashboardMainContentHitTestVisible(!_mainVM.IsAppLocked);", source);
        Assert.Contains("private void OnAppLockOverlayClick(object sender, RoutedEventArgs e)", source);
    }

    [Fact]
    public void AutoLockUsesActiveDelayThenCountdown()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("private static readonly TimeSpan AppAutoLockActiveDelay = TimeSpan.FromSeconds(10);", source);
        Assert.Contains("_appAutoLockActiveDelayTimer.Start();", source);
        Assert.Contains("StartAppAutoLockCountdown();", source);
        Assert.Contains("_appAutoLockCountdownTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, _mainVM.AppAutoLockedInterval));", source);
        Assert.Contains("return IsVisible && WindowState != WindowState.Minimized &&", source);
        Assert.Contains("HasActiveOwnedWindow()", source);
        Assert.Contains("if (IsWindowActiveForAutoLock())", source);
        Assert.Contains("ResetAppAutoLockActivity();", source);
    }

    [Fact]
    public void LockedStateUpdatesTitleAndReappliesDashboardHitTesting()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("private void RefreshActivePageTitle()", source);
        Assert.Contains("ActivePageTitle = _mainVM.IsAppLocked ? \"Locked\" : GetMainPageTitle(_activeMainPage);", source);
        Assert.Contains("SetDashboardMainContentHitTestVisible(!_mainVM.IsAppLocked);", source);
        Assert.Contains("RefreshActivePageTitle();", source);
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
