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

        Assert.Contains("public class BalloonCheckBox : CheckBox", source);
        Assert.Contains("public static readonly DependencyProperty CheckedBackgroundProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(CheckedBackground), typeof(Brush), typeof(BalloonCheckBox),", source);
        Assert.Contains("public Brush CheckedBackground", source);
        Assert.Contains("get => (Brush)GetValue(CheckedBackgroundProperty);", source);
        Assert.Contains("set => SetValue(CheckedBackgroundProperty, value);", source);
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
    public void BalloonCheckBox_UsesCheckedBackgroundWhenCheckedAndNotHovered()
    {
        RunOnStaThread(() =>
        {
            var checkBox = new BalloonCheckBox
            {
                DefaultBackground = Brushes.CornflowerBlue,
                CheckedBackground = Brushes.MintCream,
                IsChecked = true
            };

            Assert.Equal(Brushes.MintCream, checkBox.ActiveBackground);
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
