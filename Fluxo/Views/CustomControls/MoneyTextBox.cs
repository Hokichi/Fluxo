using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Fluxo.Views.CustomControls;

public class MoneyTextBox : TextBox
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(MoneyTextBox),
        new PropertyMetadata(new CornerRadius(12)));

    public static readonly DependencyProperty AllowDecimalProperty = DependencyProperty.Register(
        nameof(AllowDecimal),
        typeof(bool),
        typeof(MoneyTextBox),
        new PropertyMetadata(true));

    public static readonly DependencyProperty UseCommaAsGroupSeparatorProperty = DependencyProperty.Register(
        nameof(UseCommaAsGroupSeparator),
        typeof(bool),
        typeof(MoneyTextBox),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ShouldHighlightWhenInvalidProperty = DependencyProperty.Register(
        nameof(ShouldHighlightWhenInvalid),
        typeof(bool),
        typeof(MoneyTextBox),
        new PropertyMetadata(false));

    public MoneyTextBox()
    {
        PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(this, OnPasting);
    }

    public bool AllowDecimal
    {
        get => (bool)GetValue(AllowDecimalProperty);
        set => SetValue(AllowDecimalProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool UseCommaAsGroupSeparator
    {
        get => (bool)GetValue(UseCommaAsGroupSeparatorProperty);
        set => SetValue(UseCommaAsGroupSeparatorProperty, value);
    }

    public bool ShouldHighlightWhenInvalid
    {
        get => (bool)GetValue(ShouldHighlightWhenInvalidProperty);
        set => SetValue(ShouldHighlightWhenInvalidProperty, value);
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsValidProposedInput(Text ?? string.Empty, SelectionStart, SelectionLength, e.Text, AllowDecimal);
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!IsValidProposedInput(Text ?? string.Empty, SelectionStart, SelectionLength, pastedText, AllowDecimal))
            e.CancelCommand();
    }

    private static bool IsValidProposedInput(
        string existingText,
        int selectionStart,
        int selectionLength,
        string newText,
        bool allowDecimal)
    {
        if (string.IsNullOrEmpty(newText))
            return true;

        var proposed = selectionLength > 0
            ? existingText.Remove(selectionStart, selectionLength).Insert(selectionStart, newText)
            : existingText.Insert(selectionStart, newText);

        if (string.IsNullOrWhiteSpace(proposed))
            return true;

        var hasDecimal = false;
        foreach (var character in proposed)
        {
            if (char.IsDigit(character))
                continue;

            if (IsGroupSeparator(character))
                continue;

            if (!allowDecimal || !IsDecimalSeparator(character))
                return false;

            if (hasDecimal)
                return false;

            hasDecimal = true;
        }

        return true;
    }

    private static bool IsDecimalSeparator(char character)
    {
        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        return character == '.' || decimalSeparator.Contains(character);
    }

    private static bool IsGroupSeparator(char character)
    {
        var groupSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
        return character == ',' || groupSeparator.Contains(character);
    }
}
