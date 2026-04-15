using System.Globalization;
using System.Windows.Data;

namespace Fluxo.Converters;

public class MoneyDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return string.Empty;

        var text = value as string ?? System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var canonical = BuildCanonicalNumber(text, culture);
        return canonical.Length == 0 ? string.Empty : FormatCanonical(canonical, culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static string BuildCanonicalNumber(string text, CultureInfo culture)
    {
        var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
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

    private static string FormatCanonical(string canonical, CultureInfo culture)
    {
        var decimalIndex = canonical.IndexOf('.');
        var hasDecimal = decimalIndex >= 0;

        var integerDigits = hasDecimal ? canonical[..decimalIndex] : canonical;
        var fractionalDigits = hasDecimal ? canonical[(decimalIndex + 1)..] : string.Empty;

        if (integerDigits.Length == 0)
            integerDigits = "0";

        integerDigits = TrimLeadingZeros(integerDigits);
        var groupedInteger = GroupIntegerDigits(integerDigits, culture.NumberFormat.NumberGroupSeparator);

        if (!hasDecimal)
            return groupedInteger;

        return fractionalDigits.Length == 0
            ? groupedInteger + culture.NumberFormat.NumberDecimalSeparator
            : groupedInteger + culture.NumberFormat.NumberDecimalSeparator + fractionalDigits;
    }

    private static string TrimLeadingZeros(string digits)
    {
        var index = 0;
        while (index < digits.Length - 1 && digits[index] == '0')
            index++;

        return digits[index..];
    }

    private static string GroupIntegerDigits(string digits, string groupSeparator)
    {
        if (digits.Length <= 3)
            return digits;

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
}
