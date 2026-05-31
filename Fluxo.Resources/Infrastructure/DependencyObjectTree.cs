using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Fluxo.Resources.Infrastructure;

public static class DependencyObjectTree
{
    public static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = GetParent(current))
            if (current is T match)
                return match;

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is Visual or Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is not null)
                return visualParent;
        }

        if (element is FrameworkContentElement frameworkContentElement)
            return frameworkContentElement.Parent;

        if (element is ContentElement contentElement)
            return ContentOperations.GetParent(contentElement);

        return LogicalTreeHelper.GetParent(element);
    }
}
