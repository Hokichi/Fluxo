using System.IO;
using System.Windows;
using Fluxo.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fluxo.Tests.Services.Dialogs;

public sealed class DialogServiceTests
{
    [Fact]
    public void ShowWarning_UsesWarningIcon()
    {
        var (sut, state) = CreateSut();

        var result = sut.ShowWarning("message", "title");

        Assert.Equal(MessageBoxResult.OK, result);
        Assert.Equal(MessageBoxImage.Warning, state.LastIcon);
    }

    [Fact]
    public void ShowError_UsesErrorIcon()
    {
        var (sut, state) = CreateSut();

        var result = sut.ShowError("message", "title");

        Assert.Equal(MessageBoxResult.OK, result);
        Assert.Equal(MessageBoxImage.Error, state.LastIcon);
    }

    [Fact]
    public void ShowInformation_UsesInformationIcon()
    {
        var (sut, state) = CreateSut();

        var result = sut.ShowInformation("message", "title");

        Assert.Equal(MessageBoxResult.OK, result);
        Assert.Equal(MessageBoxImage.Information, state.LastIcon);
    }

    [Fact]
    public void ShowQuestion_UsesQuestionIcon()
    {
        var (sut, state) = CreateSut();

        var result = sut.ShowQuestion("message", "title", buttons: MessageBoxButton.YesNo);

        Assert.Equal(MessageBoxResult.OK, result);
        Assert.Equal(MessageBoxImage.Question, state.LastIcon);
        Assert.Equal(MessageBoxButton.YesNo, state.LastButtons);
    }

    [Fact]
    public void ShowDialog_ReactivatesResolvedOwnerAfterDialogCloses()
    {
        var source = File.ReadAllText(ResolveDialogServicePath());
        var showDialogBody = ExtractMethodBodyBySignature(source, "private static bool? ShowDialog(Window popup, Window? owner)");
        var reactivateOwnerBody = ExtractMethodBodyBySignature(source, "private static void ReactivateOwnerAfterDialogClose(Window? owner)");

        Assert.Contains("var resolvedOwner = popup.Owner ?? ResolveOwner(owner);", showDialogBody);
        Assert.Contains("finally", showDialogBody);
        Assert.Contains("ReactivateOwnerAfterDialogClose(resolvedOwner);", showDialogBody);
        Assert.Contains("owner.Dispatcher.BeginInvoke", reactivateOwnerBody);
        Assert.Contains("DispatcherPriority.ApplicationIdle", reactivateOwnerBody);
        Assert.Contains("owner.Activate();", reactivateOwnerBody);
        Assert.Contains("owner.Focus();", reactivateOwnerBody);
        Assert.Contains("Keyboard.Focus(owner);", reactivateOwnerBody);
        Assert.Contains("owner.WindowState == WindowState.Minimized", reactivateOwnerBody);
    }

    private static (DialogService sut, TestMessageBoxState state) CreateSut()
    {
        var state = new TestMessageBoxState();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var sut = new DialogService(serviceProvider,
            (_, _, _, buttons, icon) =>
            {
                state.LastIcon = icon;
                state.LastButtons = buttons;
                return MessageBoxResult.OK;
            });
        return (sut, state);
    }

    private sealed class TestMessageBoxState
    {
        public MessageBoxImage LastIcon { get; set; } = MessageBoxImage.None;
        public MessageBoxButton LastButtons { get; set; } = MessageBoxButton.OK;
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found in DialogService.cs.");

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
            if (depth != 0)
                continue;

            return source.Substring(openingBraceIndex + 1, index - openingBraceIndex - 1);
        }

        throw new InvalidOperationException($"Closing brace for method signature '{signatureMarker}' was not found.");
    }

    private static string ResolveDialogServicePath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                var dialogServicePath = Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Services",
                    "Dialogs",
                    "DialogService.cs");

                if (!File.Exists(dialogServicePath))
                {
                    throw new FileNotFoundException(
                        $"DialogService.cs was not found at '{dialogServicePath}'.",
                        dialogServicePath);
                }

                return dialogServicePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }
}
