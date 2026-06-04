using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BalloonButtonDependencyPropertyTests
{
    [Fact]
    public void BalloonButton_DefinesButtonTextDependencyPropertyAndClrAccessor()
    {
        var source = File.ReadAllText(ResolveBalloonButtonPath());

        Assert.Contains("public static readonly DependencyProperty ButtonTextProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ButtonText), typeof(string), typeof(BalloonButton),", source);
        Assert.Contains("public string? ButtonText", source);
        Assert.Contains("get => (string?)GetValue(ButtonTextProperty);", source);
        Assert.Contains("set => SetValue(ButtonTextProperty, value);", source);
    }

    [Fact]
    public void BalloonButton_DefinesShouldExpandDependencyPropertyDefaultingToFalse()
    {
        var source = File.ReadAllText(ResolveBalloonButtonPath());

        Assert.Contains("public static readonly DependencyProperty ShouldExpandProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ShouldExpand), typeof(bool), typeof(BalloonButton),", source);
        Assert.Contains("new PropertyMetadata(false)", source);
        Assert.Contains("public bool ShouldExpand", source);
        Assert.Contains("get => (bool)GetValue(ShouldExpandProperty);", source);
        Assert.Contains("set => SetValue(ShouldExpandProperty, value);", source);
    }

    [Fact]
    public void BalloonButton_DefinesButtonSizeAndExpandedWidthDependencyProperties()
    {
        var source = File.ReadAllText(ResolveBalloonButtonPath());

        Assert.Contains("public static readonly DependencyProperty ButtonSizeProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ButtonSize), typeof(double), typeof(BalloonButton),", source);
        Assert.Contains("public double ButtonSize", source);
        Assert.Contains("get => (double)GetValue(ButtonSizeProperty);", source);
        Assert.Contains("set => SetValue(ButtonSizeProperty, value);", source);

        Assert.Contains("public static readonly DependencyProperty ExpandedWidthProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ExpandedWidth), typeof(double), typeof(BalloonButton),", source);
        Assert.Contains("public double ExpandedWidth", source);
        Assert.Contains("get => (double)GetValue(ExpandedWidthProperty);", source);
        Assert.Contains("set => SetValue(ExpandedWidthProperty, value);", source);
    }

    private static string ResolveBalloonButtonPath() =>
        RepositoryPaths.File("Fluxo.Resources", "CustomControls", "BalloonButton.cs");
}
