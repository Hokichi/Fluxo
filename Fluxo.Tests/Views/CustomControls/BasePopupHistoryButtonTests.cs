using System;
using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BasePopupHistoryButtonTests
{
    [Fact]
    public void BasePopup_DefinesShowHistoryButtonDependencyPropertyAndClrAccessor()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());

        Assert.Contains("public static readonly DependencyProperty ShowHistoryButtonProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ShowHistoryButton), typeof(bool), typeof(BasePopup),", source);
        Assert.Contains("public bool ShowHistoryButton", source);
        Assert.Contains("get => (bool)GetValue(ShowHistoryButtonProperty);", source);
        Assert.Contains("set => SetValue(ShowHistoryButtonProperty, value);", source);
    }

    [Fact]
    public void OnApplyTemplate_WiresHistoryButtonToVirtualHandler()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public override void OnApplyTemplate()");

        Assert.Contains("WireButton(\"PART_HistoryButton\", _ => OnHistoryButtonClick());", methodBody);
    }

    [Fact]
    public void BasePopup_DeclaresVirtualHistoryButtonHandler()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());

        Assert.Contains("protected virtual void OnHistoryButtonClick()", source);
    }

    [Fact]
    public void PopupTemplate_DefinesHistoryHeaderButtonWithExpectedBindings()
    {
        var source = File.ReadAllText(ResolvePopupStylesPath());

        Assert.Contains("x:Name=\"PART_HistoryButton\"", source);
        Assert.Contains("ButtonIcon=\"{StaticResource History}\"", source);
        Assert.Contains("ButtonText=\"Show History\"", source);
        Assert.Contains("Visibility=\"{TemplateBinding ShowHistoryButton,", source);
        Assert.Contains("PopupBoolToVisibility", source);
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found in BasePopup.cs.");

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

    private static string ResolveBasePopupPath()
    {
        return Path.Combine(ResolveRepositoryRootPath(), "Fluxo.Resources", "CustomControls", "BasePopup.cs");
    }

    private static string ResolvePopupStylesPath()
    {
        return Path.Combine(ResolveRepositoryRootPath(), "Fluxo.Resources", "Resources", "Styles", "PopupStyles.xaml");
    }

    private static string ResolveRepositoryRootPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
                return currentDirectory.FullName;

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }
}
