using System.Windows.Input;

namespace Fluxo.Views.Shell.Main;

public static class MainWindowShortcutMatcher
{
    public static bool IsRunSetupWizardShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.W && modifiers == ModifierKeys.Control;
    }

    public static bool IsOpenPlanningShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.P && modifiers == ModifierKeys.Control;
    }

    public static bool IsOpenAnalyticsShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.A && modifiers == (ModifierKeys.Control | ModifierKeys.Shift);
    }

    public static bool IsCloseAnalyticsShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.Escape && modifiers == ModifierKeys.None;
    }

    public static bool IsOpenSearchShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.F && modifiers == ModifierKeys.Control;
    }

    public static bool IsOpenQuickAddShortcut(Key key, ModifierKeys modifiers)
    {
        return key == Key.N && modifiers == ModifierKeys.Control;
    }
}
