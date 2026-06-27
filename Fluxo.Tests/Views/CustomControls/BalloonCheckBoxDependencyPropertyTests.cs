using System.IO;
using System.Windows;
using System.Windows.Media;
using Fluxo.Resources.CustomControls;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BalloonCheckBoxDependencyPropertyTests
{
    [Fact]
    public void BalloonCheckBox_DefinesCheckedBackgroundDependencyPropertyAndClrAccessor()
    {
        var source = File.ReadAllText(ResolveBalloonCheckBoxPath());

        Assert.Contains("public class BalloonCheckBox : BalloonControl", source);
        Assert.Contains("public static readonly DependencyProperty CheckedBackgroundProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(CheckedBackground), typeof(Brush), typeof(BalloonCheckBox),", source);
        Assert.Contains("public Brush CheckedBackground", source);
        Assert.Contains("get => (Brush)GetValue(CheckedBackgroundProperty);", source);
        Assert.Contains("set => SetValue(CheckedBackgroundProperty, value);", source);
    }

    [Fact]
    public void BalloonCheckBox_ClickTogglesAndRaisesEvents()
    {
        RunOnStaThread(() =>
        {
            var checkBox = new TestBalloonCheckBox();
            var checkedCount = 0;
            var uncheckedCount = 0;
            checkBox.Checked += (_, _) => checkedCount++;
            checkBox.Unchecked += (_, _) => uncheckedCount++;

            checkBox.InvokeClick();
            Assert.True(checkBox.IsChecked);
            Assert.Equal(1, checkedCount);

            checkBox.InvokeClick();
            Assert.False(checkBox.IsChecked);
            Assert.Equal(1, uncheckedCount);
        });
    }

    [Fact]
    public void BalloonCheckBox_DefinesCheckedAndUncheckedIconTextDependencyProperties()
    {
        var source = File.ReadAllText(ResolveBalloonCheckBoxPath());
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo.Resources", "Resources", "Styles", "ButtonStyles.xaml"));

        Assert.Contains("public static readonly DependencyProperty UncheckedIconProperty =", source);
        Assert.Contains("public static readonly DependencyProperty CheckedIconProperty =", source);
        Assert.Contains("public static readonly DependencyProperty UncheckedTextProperty =", source);
        Assert.Contains("public static readonly DependencyProperty CheckedTextProperty =", source);
        Assert.Contains("public object? UncheckedIcon", source);
        Assert.Contains("public object? CheckedIcon", source);
        Assert.Contains("public string? UncheckedText", source);
        Assert.Contains("public string? CheckedText", source);
        Assert.Contains("Text=\"{TemplateBinding ButtonText}\"", xaml);
        Assert.DoesNotContain("ActiveButtonText", xaml);
    }

    [Fact]
    public void BalloonCheckBox_NoLongerExposesExpandedWidthOverride()
    {
        var source = File.ReadAllText(ResolveBalloonCheckBoxPath());
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo.Resources", "Resources", "Styles", "ButtonStyles.xaml"));

        Assert.DoesNotContain("ExpandedWidth", source);
        Assert.DoesNotContain("ExpandedWidth", xaml);
    }

    [Fact]
    public void BalloonCheckBox_StoresCheckedBackground()
    {
        RunOnStaThread(() =>
        {
            var checkBox = new BalloonCheckBox { CheckedBackground = Brushes.MintCream };
            Assert.Equal(Brushes.MintCream, checkBox.CheckedBackground);
        });
    }

    [Fact]
    public void BalloonCheckBox_CoercesShouldExpandFalse_WhenShouldShowTextIsTrue()
    {
        RunOnStaThread(() =>
        {
            var checkBox = new BalloonCheckBox
            {
                ShouldExpand = true,
                ShouldShowText = true
            };

            Assert.False(checkBox.ShouldExpand);
        });
    }

    private static string ResolveBalloonCheckBoxPath() =>
        RepositoryPaths.File("Fluxo.Resources", "CustomControls", "BalloonCheckBox.cs");

    private sealed class TestBalloonCheckBox : BalloonCheckBox
    {
        public void InvokeClick() => OnClick();
    }

    private static string ExtractSection(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker '{startMarker}' was not found.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"End marker '{endMarker}' was not found.");
        return source[start..end];
    }

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
