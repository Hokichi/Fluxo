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
}
