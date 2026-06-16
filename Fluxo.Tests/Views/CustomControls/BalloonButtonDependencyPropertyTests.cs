using System.IO;
using System.Windows;
using Fluxo.Resources.CustomControls;
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
        Assert.Contains("new FrameworkPropertyMetadata(false", source);
        Assert.Contains("CoerceShouldExpand", source);
        Assert.Contains("public bool ShouldExpand", source);
        Assert.Contains("get => (bool)GetValue(ShouldExpandProperty);", source);
        Assert.Contains("set => SetValue(ShouldExpandProperty, value);", source);
    }

    [Fact]
    public void BalloonButton_DefinesShouldShowTextDependencyPropertyDefaultingToFalse()
    {
        var source = File.ReadAllText(ResolveBalloonButtonPath());

        Assert.Contains("public static readonly DependencyProperty ShouldShowTextProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ShouldShowText), typeof(bool), typeof(BalloonButton),", source);
        Assert.Contains("new PropertyMetadata(false, OnShouldShowTextChanged)", source);
        Assert.Contains("public bool ShouldShowText", source);
        Assert.Contains("get => (bool)GetValue(ShouldShowTextProperty);", source);
        Assert.Contains("set => SetValue(ShouldShowTextProperty, value);", source);
    }

    [Fact]
    public void BalloonButton_DefinesButtonSizeDependencyProperty()
    {
        var source = File.ReadAllText(ResolveBalloonButtonPath());

        Assert.Contains("public static readonly DependencyProperty ButtonSizeProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ButtonSize), typeof(double), typeof(BalloonButton),", source);
        Assert.Contains("public double ButtonSize", source);
        Assert.Contains("get => (double)GetValue(ButtonSizeProperty);", source);
        Assert.Contains("set => SetValue(ButtonSizeProperty, value);", source);

        Assert.DoesNotContain("ExpandedWidth", source);
    }

    [Fact]
    public void BalloonButton_TracksActiveBackgroundForForegroundConversion()
    {
        var source = File.ReadAllText(ResolveBalloonButtonPath());

        Assert.Contains("public static readonly DependencyProperty ActiveBackgroundProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ActiveBackground), typeof(Brush), typeof(BalloonButton),", source);
        Assert.Contains("public Brush ActiveBackground", source);
        Assert.Contains("private set => SetValue(ActiveBackgroundProperty, value);", source);
        Assert.Contains("ActiveBackground = HoveredBackground;", source);
        Assert.Contains("ActiveBackground = DefaultBackground;", source);
    }

    [Fact]
    public void BalloonButton_CoercesShouldExpandFalse_WhenShouldShowTextIsTrue()
    {
        RunOnStaThread(() =>
        {
            var button = new BalloonButton
            {
                ShouldExpand = true,
                ShouldShowText = true
            };

            Assert.False(button.ShouldExpand);
        });
    }

    [Fact]
    public void BalloonButton_CalculatesAutoOpenWidthFromTextAndChrome()
    {
        var width = BalloonButton.CalculateAutoOpenWidth(
            buttonSize: 28,
            iconSize: 8,
            padding: new Thickness(6, 0, 10, 0),
            textWidth: 52,
            textMargin: new Thickness(8, 0, 0, 0));

        Assert.Equal(88, width);
    }

    [Fact]
    public void BalloonButton_UsesButtonSizeAsMinimumOpenWidth()
    {
        Assert.Equal(28, BalloonButton.ResolveOpenWidth(28, 20));
    }

    [Fact]
    public void BalloonButton_UsesAutoOpenWidth_WhenContentNeedsMoreSpace()
    {
        Assert.Equal(150, BalloonButton.ResolveOpenWidth(28, 150));
    }

    [Fact]
    public void BalloonButton_UsesAutoOpenWidth()
    {
        Assert.Equal(94, BalloonButton.ResolveOpenWidth(28, 94));
    }

    [Fact]
    public void BalloonButton_MeasuresShouldShowTextContent()
    {
        RunOnStaThread(() =>
        {
            var button = new BalloonButton
            {
                ButtonSize = 28,
                ButtonText = "New Transaction",
                FontSize = 12,
                IconSize = 18,
                Padding = new Thickness(6, 0, 10, 0),
                ShouldShowText = true
            };

            Assert.True(button.GetEffectiveOpenWidth() > 96);
        });
    }

    [Fact]
    public void BalloonButton_MeasuresExpansionContent()
    {
        RunOnStaThread(() =>
        {
            var button = new BalloonButton
            {
                ButtonSize = 28,
                ButtonText = "New Transaction",
                FontSize = 12,
                IconSize = 18,
                Padding = new Thickness(6, 0, 10, 0),
                ShouldExpand = true
            };

            Assert.True(button.GetEffectiveOpenWidth() > 96);
        });
    }

    private static string ResolveBalloonButtonPath() =>
        RepositoryPaths.File("Fluxo.Resources", "CustomControls", "BalloonButton.cs");

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
