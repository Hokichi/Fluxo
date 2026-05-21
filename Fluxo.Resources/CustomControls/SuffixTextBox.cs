using System.Windows;

namespace Fluxo.Resources.CustomControls;

public class SuffixTextBox : MoneyTextBox
{
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(
        nameof(Suffix),
        typeof(string),
        typeof(SuffixTextBox),
        new PropertyMetadata(string.Empty));

    public string Suffix
    {
        get => (string)GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }
}
