using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AppUnlockPopupTests
{
    [Fact]
    public void PopupUsesPasswordBoxAndInlineValidation()
    {
        var xaml = File.ReadAllText(ResolvePath("Fluxo", "Views", "Popups", "AppUnlockPopup.xaml"));

        Assert.Contains("x:Name=\"UnlockPasswordBox\"", xaml);
        Assert.Contains("PasswordChanged=\"OnUnlockPasswordBoxPasswordChanged\"", xaml);
        Assert.Contains("x:Name=\"ValidationText\"", xaml);
        Assert.Contains("Password does not match.", xaml);
        Assert.Contains("Content=\"Unlock\"", xaml);
    }

    [Fact]
    public void PopupAcceptsEnterAndKeepsDialogOpenOnMismatch()
    {
        var source = File.ReadAllText(ResolvePath("Fluxo", "Views", "Popups", "AppUnlockPopup.xaml.cs"));

        Assert.Contains("e.Key == Key.Enter", source);
        Assert.Contains("_tryUnlock(UnlockPasswordBox.Password)", source);
        Assert.Contains("ValidationText.Visibility = Visibility.Visible;", source);
        Assert.Contains("DialogResult = true;", source);
        Assert.Contains("DialogResult = false;", source);
    }

    private static string ResolvePath(params string[] parts)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Fluxo.sln")) ||
                File.Exists(Path.Combine(currentDirectory.FullName, "Fluxo.slnx")))
            {
                return Path.Combine([currentDirectory.FullName, ..parts]);
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
