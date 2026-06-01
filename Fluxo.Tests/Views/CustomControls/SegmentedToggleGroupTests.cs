using System;
using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class SegmentedToggleGroupTests
{
    [Fact]
    public void SegmentedToggleGroup_DefinesSelectionAndSpacingDependencyProperties()
    {
        var source = File.ReadAllText(ResolveCustomControlPath("SegmentedToggleGroup.cs"));

        Assert.Contains("public static readonly DependencyProperty OptionSpacingProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(OptionSpacing), typeof(double), typeof(SegmentedToggleGroup)", source);
        Assert.Contains("public double OptionSpacing", source);
        Assert.Contains("public static readonly DependencyProperty SelectedValueProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(SegmentedToggleGroup)", source);
        Assert.Contains("FrameworkPropertyMetadataOptions.BindsTwoWayByDefault", source);
        Assert.Contains("public object? SelectedValue", source);
    }

    [Fact]
    public void SegmentedToggleGroup_DeclaresBubbledOptionSelectedEvent()
    {
        var source = File.ReadAllText(ResolveCustomControlPath("SegmentedToggleGroup.cs"));

        Assert.Contains("public static readonly RoutedEvent OptionSelectedEvent =", source);
        Assert.Contains("EventManager.RegisterRoutedEvent(nameof(OptionSelected), RoutingStrategy.Bubble", source);
        Assert.Contains("public event RoutedEventHandler OptionSelected", source);
        Assert.Contains("RaiseEvent(new RoutedEventArgs(OptionSelectedEvent, option));", source);
    }

    [Fact]
    public void SegmentedToggleGroup_IgnoresNullOptionValuesWhenSynchronizingSelection()
    {
        var source = File.ReadAllText(ResolveCustomControlPath("SegmentedToggleGroup.cs"));

        Assert.Contains("if (selectedValue is not null)", source);
        Assert.Contains("if (SelectedValue is null)", source);
        Assert.Contains("if (optionValue is null)", source);
    }

    [Fact]
    public void SegmentedToggleOption_DefinesSelectableOptionProperties()
    {
        var source = File.ReadAllText(ResolveCustomControlPath("SegmentedToggleOption.cs"));

        Assert.Contains("public static readonly DependencyProperty IsSelectedProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(SegmentedToggleOption)", source);
        Assert.Contains("FrameworkPropertyMetadataOptions.BindsTwoWayByDefault", source);
        Assert.Contains("public bool IsSelected", source);
        Assert.Contains("public static readonly DependencyProperty ValueProperty =", source);
        Assert.Contains("public object? Value", source);
        Assert.Contains("public static readonly DependencyProperty SelectOnClickProperty =", source);
        Assert.Contains("public bool SelectOnClick", source);
        Assert.Contains("protected override void OnClick()", source);
        Assert.Contains("SetCurrentValue(IsSelectedProperty, true);", source);
    }

    [Fact]
    public void SegmentedTogglePanel_ArrangesVisibleOptionsWithEqualWidthsAfterSpacing()
    {
        var source = File.ReadAllText(ResolveCustomControlPath("SegmentedTogglePanel.cs"));

        Assert.Contains("public sealed class SegmentedTogglePanel : Panel", source);
        Assert.Contains("var totalSpacing = spacing * Math.Max(0, visibleChildrenCount - 1);", source);
        Assert.Contains("var itemWidth = Math.Max(0, (finalSize.Width - totalSpacing) / visibleChildrenCount);", source);
        Assert.Contains("child.Arrange(new Rect(x, 0, itemWidth, finalSize.Height));", source);
        Assert.Contains("ItemsControl.GetItemsOwner(this) is SegmentedToggleGroup group", source);
    }

    [Fact]
    public void ButtonStyles_DefinesImplicitSegmentedToggleTemplates()
    {
        var source = File.ReadAllText(ResolveButtonStylesPath());

        Assert.Contains("TargetType=\"{x:Type c:SegmentedToggleGroup}\"", source);
        Assert.Contains("TargetType=\"{x:Type c:SegmentedToggleOption}\"", source);
        Assert.Contains("ItemsPresenter", source);
        Assert.Contains("<c:SegmentedTogglePanel />", source);
        Assert.Contains("Brush.Background.Surface", source);
        Assert.Contains("Brush.Border.Subtle", source);
        Assert.Contains("Brush.Background.Hover", source);
        Assert.Contains("Brush.Mint", source);
    }

    private static string ResolveCustomControlPath(string fileName)
    {
        return Path.Combine(ResolveRepositoryRootPath(), "Fluxo.Resources", "CustomControls", fileName);
    }

    private static string ResolveButtonStylesPath()
    {
        return Path.Combine(ResolveRepositoryRootPath(), "Fluxo.Resources", "Resources", "Styles", "ButtonStyles.xaml");
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
