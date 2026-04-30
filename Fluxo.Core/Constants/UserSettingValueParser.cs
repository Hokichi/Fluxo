using Fluxo.Core.Enums;

namespace Fluxo.Core.Constants;

public static class UserSettingValueParser
{
    public static bool ParseBool(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;
    }

    public static AppCloseBehavior ParseCloseBehavior(string? value, AppCloseBehavior defaultValue = AppCloseBehavior.Exit)
    {
        return Enum.TryParse<AppCloseBehavior>(value, ignoreCase: true, out var parsedValue) &&
               Enum.IsDefined(parsedValue)
            ? parsedValue
            : defaultValue;
    }
}
