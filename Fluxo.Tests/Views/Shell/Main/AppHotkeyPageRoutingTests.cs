using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class AppHotkeyPageRoutingTests
{
    [Fact]
    public void Calendar_ArrowKeysNavigateCalendar()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Main",
            "Pages",
            "Calendar.xaml.cs"));

        Assert.Contains("PreviewKeyDown += OnCalendarPreviewKeyDown;", source);
        Assert.Contains("case Key.Left:", source);
        Assert.Contains("case Key.Right:", source);
        Assert.Contains("case Key.Up:", source);
        Assert.Contains("case Key.Down:", source);
        Assert.Contains("await _viewModel.SelectRelativeDateAsync(-1);", source);
        Assert.Contains("await _viewModel.SelectRelativeDateAsync(1);", source);
        Assert.Contains("await _viewModel.SelectRelativeDateAsync(-7);", source);
        Assert.Contains("await _viewModel.SelectRelativeDateAsync(7);", source);
    }

    [Fact]
    public void Settings_UpDownNavigateTabs()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "SettingsPopup.xaml.cs"));

        Assert.Contains("protected override void OnPreviewKeyDown(KeyEventArgs e)", source);
        Assert.Contains("case Key.Up:", source);
        Assert.Contains("case Key.Down:", source);
        Assert.Contains("NavigateSettingsTabAsync(-1);", source);
        Assert.Contains("NavigateSettingsTabAsync(1);", source);
    }

    [Fact]
    public void SetupWizard_EnterMovesToNextStep()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Wizard",
            "QuickSetupWizard.xaml.cs"));

        Assert.Contains("protected override void OnPreviewKeyDown(KeyEventArgs e)", source);
        Assert.Contains("if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)", source);
        Assert.Contains("OnNextClick(this, new RoutedEventArgs(Button.ClickEvent));", source);
    }

    [Fact]
    public void Ledger_ExposesShortcutMethods()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Main",
            "Pages",
            "Ledger.xaml.cs"));

        Assert.Contains("public async void ExportDataFromShortcutAsync()", source);
        Assert.Contains("public async void ClearFiltersFromShortcutAsync()", source);
        Assert.Contains("public async void ApplyAmountSortDirectionFromShortcutAsync(LedgerAmountSortDirection direction)", source);
        Assert.Contains("await ExportDataAsync();", source);
        Assert.Contains("await ShowFilterRefreshToastAsync(viewModel.ClearFilters);", source);
        Assert.Contains("viewModel.AmountSortDirection = direction;", source);
    }
}
