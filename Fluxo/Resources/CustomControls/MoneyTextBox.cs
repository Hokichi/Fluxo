using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Fluxo.Resources.CustomControls;

public class MoneyTextBox : TextBox
{
    private bool _isInternalUpdate;

    public MoneyTextBox()
    {
        PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(this, OnPasting);
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsValidProposedInput(Text ?? string.Empty, SelectionStart, SelectionLength, e.Text);
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!IsValidProposedInput(Text ?? string.Empty, SelectionStart, SelectionLength, pastedText))
            e.CancelCommand();
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);

        if (_isInternalUpdate || IsReadOnly)
            return;

        var originalText = Text ?? string.Empty;
        if (originalText.Length == 0)
            return;

        var canonical = BuildCanonicalNumber(originalText);
        var formatted = FormatCanonical(canonical);

        if (string.Equals(originalText, formatted, StringComparison.Ordinal))
            return;

        var canonicalCaretIndex = GetCanonicalCaretIndex(originalText, SelectionStart);
        var newCaretIndex = MapCanonicalIndexToFormatted(formatted, canonicalCaretIndex);

        _isInternalUpdate = true;
        try
        {
            Text = formatted;
            CaretIndex = Math.Min(newCaretIndex, formatted.Length);
            SelectionLength = 0;
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    private static bool IsValidProposedInput(string existingText, int selectionStart, int selectionLength, string newText)
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

            if (!IsDecimalSeparator(character))
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

    private static string BuildCanonicalNumber(string text)
    {
        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var hasDecimal = false;
        var characters = new List<char>(text.Length);

        foreach (var character in text)
        {
            if (char.IsDigit(character))
            {
                characters.Add(character);
                continue;
            }

            if (!hasDecimal && (character == '.' || decimalSeparator.Contains(character)))
            {
                characters.Add('.');
                hasDecimal = true;
            }
        }

        return new string(characters.ToArray());
    }

    private static string FormatCanonical(string canonical)
    {
        if (canonical.Length == 0)
            return string.Empty;

        var decimalIndex = canonical.IndexOf('.');
        var hasDecimal = decimalIndex >= 0;

        var integerDigits = hasDecimal ? canonical[..decimalIndex] : canonical;
        var fractionalDigits = hasDecimal ? canonical[(decimalIndex + 1)..] : string.Empty;

        if (integerDigits.Length == 0)
            integerDigits = "0";

        integerDigits = TrimLeadingZeros(integerDigits);
        var groupedInteger = GroupIntegerDigits(integerDigits);

        if (!hasDecimal)
            return groupedInteger;

        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        return fractionalDigits.Length == 0
            ? groupedInteger + decimalSeparator
            : groupedInteger + decimalSeparator + fractionalDigits;
    }

    private static string TrimLeadingZeros(string digits)
    {
        var index = 0;
        while (index < digits.Length - 1 && digits[index] == '0')
            index++;

        return digits[index..];
    }

    private static string GroupIntegerDigits(string digits)
    {
        if (digits.Length <= 3)
            return digits;

        var groupSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
        var builder = new System.Text.StringBuilder(digits.Length + (digits.Length / 3));

        var headLength = digits.Length % 3;
        if (headLength == 0)
            headLength = 3;

        builder.Append(digits[..headLength]);

        for (var i = headLength; i < digits.Length; i += 3)
        {
            builder.Append(groupSeparator);
            builder.Append(digits, i, 3);
        }

        return builder.ToString();
    }

    private static int GetCanonicalCaretIndex(string text, int caretIndex)
    {
        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var canonicalIndex = 0;
        var hasDecimal = false;

        for (var i = 0; i < Math.Min(caretIndex, text.Length); i++)
        {
            var character = text[i];
            if (char.IsDigit(character))
            {
                canonicalIndex++;
                continue;
            }

            if (!hasDecimal && (character == '.' || decimalSeparator.Contains(character)))
            {
                canonicalIndex++;
                hasDecimal = true;
            }
        }

        return canonicalIndex;
    }

    private static int MapCanonicalIndexToFormatted(string formattedText, int canonicalIndex)
    {
        if (canonicalIndex <= 0)
            return 0;

        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var seen = 0;

        for (var i = 0; i < formattedText.Length; i++)
        {
            var character = formattedText[i];
            if (!char.IsDigit(character) && !decimalSeparator.Contains(character))
                continue;

            seen++;
            if (seen >= canonicalIndex)
                return i + 1;
        }

        return formattedText.Length;
    }
}
