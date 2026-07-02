using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class CalendarLayoutTests
{
    [Fact]
    public void CalendarLayout_DateCellsBindOnlyToDayNumber()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("Text=\"{Binding DayNumber}\"", xaml);
        Assert.DoesNotContain("Calendar cell amount", xaml);
        Assert.DoesNotContain("Expense count", xaml);
    }

    [Fact]
    public void CalendarLayout_DayButtonsKeepDayItemDataContextForStyleTriggers()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("Command=\"{Binding DataContext.SelectDateCommand, RelativeSource={RelativeSource AncestorType=UserControl}}\"", xaml);
        Assert.Contains("CommandParameter=\"{Binding}\"", xaml);
        Assert.DoesNotContain("DataContext=\"{Binding DataContext, RelativeSource={RelativeSource AncestorType=UserControl}}\"", xaml);
        Assert.DoesNotContain("CommandParameter=\"{Binding Tag, RelativeSource={RelativeSource AncestorType=Grid}}\"", xaml);
    }

    [Fact]
    public void CalendarLayout_MonthHeaderContainsBalloonMonthNavigationButtonsAndTodayButton()
    {
        var xaml = LoadCalendarXaml();
        var document = XDocument.Parse(xaml);
        var presentation = document.Root!.Name.Namespace;
        var calendarRoot = document.Root
            .Elements(presentation + "Grid")
            .Single(element => (string?)element.Attribute("Margin") == "12");
        var leftColumn = calendarRoot
            .Elements(presentation + "Grid")
            .Single(element => element.Attribute("Grid.Column") is null);
        var monthHeader = leftColumn
            .Elements(presentation + "Border")
            .Single(element => element.Attribute("Grid.Row") is null);
        var icons = monthHeader.Descendants().Attributes("ButtonIcon").Select(attribute => attribute.Value).ToArray();

        Assert.Contains(monthHeader.Descendants().Attributes("Text"),
            attribute => attribute.Value == "{Binding VisibleMonthLabel}");
        Assert.Contains("{StaticResource AngleUp}", icons);
        Assert.Contains("{StaticResource CalendarTodayOutline}", icons);
        Assert.Contains("{StaticResource AngleDown}", icons);
        Assert.True(
            Array.IndexOf(icons, "{StaticResource AngleUp}") <
            Array.IndexOf(icons, "{StaticResource CalendarTodayOutline}"));
        Assert.True(
            Array.IndexOf(icons, "{StaticResource CalendarTodayOutline}") <
            Array.IndexOf(icons, "{StaticResource AngleDown}"));
        Assert.Contains(monthHeader.Descendants().Attributes("Command"), attribute => attribute.Value.Contains("NavigateToPreviousMonthCommand", StringComparison.Ordinal));
        Assert.Contains(monthHeader.Descendants().Attributes("Click"), attribute => attribute.Value == "OnCalendarTodayButtonClick");
        Assert.Contains(monthHeader.Descendants().Attributes("Command"), attribute => attribute.Value.Contains("NavigateToNextMonthCommand", StringComparison.Ordinal));
        Assert.Contains(monthHeader.Descendants().Attributes("IsEnabled"), attribute => attribute.Value.Contains("IsAtCurrentDay", StringComparison.Ordinal));
    }

    [Fact]
    public void CalendarLayout_CtrlHomeAndTodayButtonShareCurrentDayNavigationHelper()
    {
        var codeBehind = LoadCalendarCodeBehind();
        var keyHandler = ExtractMethod(codeBehind, "private async void OnCalendarPreviewKeyDown");
        var todayHandler = ExtractMethod(codeBehind, "private async void OnCalendarTodayButtonClick");
        var helper = ExtractMethod(codeBehind, "private async Task NavigateToCurrentDateFromUserAsync");

        Assert.Contains("await NavigateToCurrentDateFromUserAsync();", keyHandler);
        Assert.Contains("await NavigateToCurrentDateFromUserAsync();", todayHandler);
        Assert.Contains("if (_viewModel.IsAtCurrentDay)", helper);
        Assert.Contains("ResetCalendarScrollOffset();", helper);
        Assert.Contains("await _viewModel.SelectCurrentDateAsync();", helper);
    }

    [Fact]
    public void CalendarLayout_DoesNotSetCanContentScroll()
    {
        var xaml = LoadCalendarXaml();

        Assert.DoesNotContain("CanContentScroll", xaml);
    }

    [Fact]
    public void CalendarLayout_ResizesCalendarCardWithWindowLayoutState()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("x:Key=\"CalendarColumnLayoutStyle\"", xaml);
        Assert.Contains("x:Key=\"CalendarGridCardStyle\"", xaml);
        Assert.Contains("<Setter Property=\"Width\" Value=\"480\" />", xaml);
        Assert.Contains("<Setter Property=\"Height\" Value=\"480\" />", xaml);
        Assert.Contains("<Setter Property=\"Width\" Value=\"570\" />", xaml);
        Assert.Contains("<Setter Property=\"Height\" Value=\"570\" />", xaml);
        Assert.Contains("Binding=\"{Binding UseExpandedCalendarLayout, ElementName=CalendarRoot}\" Value=\"True\"", xaml);
        Assert.Contains("Style=\"{StaticResource CalendarGridCardStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource CalendarColumnLayoutStyle}\"", xaml);
    }

    [Fact]
    public void CalendarLayout_UpdatesBufferedWeekStackWhenViewportSizeIsKnown()
    {
        var xaml = LoadCalendarXaml();
        var codeBehind = LoadCalendarCodeBehind();

        Assert.Contains("x:Name=\"CalendarWeekViewport\"", xaml);
        Assert.Contains("SizeChanged=\"OnCalendarWeekViewportSizeChanged\"", xaml);
        Assert.Contains("x:Name=\"CalendarWeekSurface\"", xaml);
        Assert.Contains("ClipToBounds=\"False\"", xaml);
        Assert.Contains("Width=\"{Binding ActualWidth, ElementName=CalendarWeekViewport}\"", xaml);
        Assert.Contains("private void OnCalendarWeekViewportSizeChanged(object sender, SizeChangedEventArgs e)", codeBehind);
        Assert.Contains("QueueCalendarScrollOffsetReset();", codeBehind);
        Assert.Contains("DispatcherPriority.Loaded", codeBehind);
        Assert.Contains("CalendarWeekViewport.SizeChanged -= OnCalendarWeekViewportSizeChanged;", codeBehind);
        Assert.Contains("private const int CalendarBufferWeekCount = 2;", codeBehind);
        Assert.Contains("private const int CalendarBufferedWeekCount = CalendarFrameWeekCount + CalendarBufferWeekCount * 2;", codeBehind);
        Assert.Contains("CalendarWeekItemsControl.Height = rowHeight * CalendarBufferedWeekCount;", codeBehind);
        Assert.Contains("CalendarWeekTranslateTransform.Y = -(CalendarBufferWeekCount * rowHeight) - _calendarScrollOffset;", codeBehind);
    }

    [Fact]
    public void CalendarLayout_FocusesScrollableCalendarForKeyboardNavigation()
    {
        var xaml = LoadCalendarXaml();
        var codeBehind = LoadCalendarCodeBehind();
        var dayTemplate = ExtractSection(xaml, "x:Key=\"CalendarDayTemplate\"", "x:Key=\"CalendarWeekTemplate\"");

        Assert.Contains("Focusable=\"True\"", xaml);
        Assert.Contains("x:Name=\"CalendarWeekViewport\"", xaml);
        Assert.Contains("FocusCalendarKeyboardTarget();", codeBehind);
        Assert.Contains("Keyboard.Focus(CalendarWeekViewport);", codeBehind);
        Assert.Contains("Click=\"OnCalendarDayButtonClick\"", dayTemplate);
        Assert.Contains("Focusable=\"False\"", dayTemplate);
        Assert.Contains("private void OnCalendarDayButtonClick(object sender, RoutedEventArgs e)", codeBehind);
        Assert.Contains("DispatcherPriority.Input", codeBehind);
    }

    [Fact]
    public void CalendarLayout_AnimatesMouseWheelCalendarScrollBeforeRecyclingRows()
    {
        var codeBehind = LoadCalendarCodeBehind();
        var mouseWheelHandler = ExtractMethod(codeBehind, "private void OnCalendarMouseWheel");
        var renderingHandler = ExtractMethod(codeBehind, "private void OnCalendarScrollRendering");

        Assert.Contains("CompositionTarget.Rendering += OnCalendarScrollRendering;", codeBehind);
        Assert.Contains("CompositionTarget.Rendering -= OnCalendarScrollRendering;", codeBehind);
        Assert.Contains("_targetCalendarScrollOffset -= e.Delta * MouseWheelPixelsPerDelta;", codeBehind);
        Assert.Contains("StartCalendarScrollAnimation();", codeBehind);
        Assert.Contains("1d - Math.Exp(-CalendarScrollSmoothing * elapsed.TotalSeconds)", codeBehind);
        Assert.Contains("_calendarScrollOffset += distanceToTarget * progress;", codeBehind);
        Assert.DoesNotContain("NormalizeCalendar", mouseWheelHandler);
        Assert.Contains("NormalizeCalendarScrollOffset(rowHeight);", renderingHandler);
    }

    [Fact]
    public void CalendarLayout_UsesPageSizeFallbackForExpandedLayout()
    {
        var xaml = LoadCalendarXaml();
        var codeBehind = LoadCalendarCodeBehind();

        Assert.Contains("UseExpandedCalendarLayoutProperty", codeBehind);
        Assert.Contains("ExpandedLayoutMinWidth", codeBehind);
        Assert.Contains("ActualWidth >= ExpandedLayoutMinWidth", codeBehind);
        Assert.Contains("GetIsWindowLayoutMaximized()", codeBehind);
        Assert.Contains("SizeChanged += OnCalendarSizeChanged;", codeBehind);
        Assert.Contains("Loaded += OnCalendarLoaded;", codeBehind);
        Assert.Contains("Binding=\"{Binding UseExpandedCalendarLayout, ElementName=CalendarRoot}\" Value=\"True\"", xaml);
        Assert.DoesNotContain("Binding=\"{Binding IsWindowLayoutMaximized, RelativeSource={RelativeSource AncestorType=Window}}\" Value=\"True\"", xaml);
    }

    private static string ExtractSection(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");

        var endSearchStart = start + startMarker.Length;
        var end = content.IndexOf(endMarker, endSearchStart, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find end marker '{endMarker}'.");

        return content[start..(end + endMarker.Length)];
    }

    private static string ExtractMethod(string content, string signature)
    {
        var start = content.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find method '{signature}'.");

        var bodyStart = content.IndexOf('{', start);
        Assert.True(bodyStart >= 0, $"Could not find method body for '{signature}'.");

        var depth = 0;
        for (var index = bodyStart; index < content.Length; index++)
        {
            if (content[index] == '{')
                depth++;
            else if (content[index] == '}')
                depth--;

            if (depth == 0)
                return content[start..(index + 1)];
        }

        throw new InvalidOperationException($"Could not parse method '{signature}'.");
    }

    private static string LoadCalendarXaml()
    {
        return File.ReadAllText(ResolveCalendarPath("Calendar.xaml"));
    }

    private static string LoadCalendarCodeBehind()
    {
        return File.ReadAllText(ResolveCalendarPath("Calendar.xaml.cs"));
    }

    private static string ResolveCalendarPath(string fileName)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Fluxo.slnx")))
            {
                return Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "Shell",
                    "Main",
                    "Pages",
                    fileName);
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Fluxo repository root.");
    }
}
