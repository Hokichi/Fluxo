using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;

namespace Fluxo.ViewModels.Entities;

/// <summary>
/// Strongly-typed ViewModel for the Settings popup.
/// Each property maps to a named key in AppSettings.
/// Validation runs on the whole object — the "Save" button stays disabled
/// until all fields are valid.
/// </summary>
public partial class AppSettingVM : BaseEntityVM
{
    // ── Currency ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Currency is required.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO 4217 code (e.g. USD, VND).")]
    private string _currency = "USD";

    // ── Notifications ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 30, ErrorMessage = "Notification lead time must be between 0 and 30 days.")]
    private int _notificationLeadDays = 3;

    // ── Default entry day ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 28, ErrorMessage = "Default entry day must be between 1 and 28.")]
    private int _defaultEntryDay = 1;

    // ── Theme ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Theme is required.")]
    [CustomValidation(typeof(AppSettingVM), nameof(ValidateTheme))]
    private string _theme = "system";

    // ── AI integration (optional) ─────────────────────────────────────────────

    /// <summary>
    /// API key is stored as-is (no length restriction).
    /// Left empty when the user hasn't configured AI features.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAiConfigured))]
    [NotifyPropertyChangedFor(nameof(AiStatusLabel))]
    private string? _aiApiKey;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AppSettingVM), nameof(ValidateAiProvider))]
    [NotifyPropertyChangedFor(nameof(IsAiConfigured))]
    [NotifyPropertyChangedFor(nameof(AiStatusLabel))]
    private string? _aiProvider;

    // ── Derived helpers ───────────────────────────────────────────────────────

    public bool IsAiConfigured =>
        !string.IsNullOrWhiteSpace(AiApiKey) &&
        !string.IsNullOrWhiteSpace(AiProvider);

    public string AiStatusLabel => IsAiConfigured
        ? $"✓ AI enabled ({AiProvider})"
        : "AI insights are disabled — add an API key to enable.";

    /// <summary>All valid theme identifiers accepted by the ThemeService.</summary>
    public static IReadOnlyList<string> ValidThemes { get; } = ["light", "dark", "system"];

    /// <summary>All supported AI providers.</summary>
    public static IReadOnlyList<string> ValidAiProviders { get; } = ["anthropic", "openai"];

    public AppSettingVM() => ValidateAllProperties();

    // ── Custom validators ─────────────────────────────────────────────────────

    public static ValidationResult? ValidateTheme(string? value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new ValidationResult("Theme is required.");
        if (!ValidThemes.Contains(value, StringComparer.OrdinalIgnoreCase))
            return new ValidationResult($"Theme must be one of: {string.Join(", ", ValidThemes)}.");
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateAiProvider(string? value, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is not AppSettingVM vm) return ValidationResult.Success;

        // Provider is only required when an API key has been entered.
        if (!string.IsNullOrWhiteSpace(vm.AiApiKey) && string.IsNullOrWhiteSpace(value))
            return new ValidationResult("Select a provider (anthropic or openai) when an API key is provided.");

        if (!string.IsNullOrWhiteSpace(value) &&
            !ValidAiProviders.Contains(value, StringComparer.OrdinalIgnoreCase))
            return new ValidationResult($"Provider must be one of: {string.Join(", ", ValidAiProviders)}.");

        return ValidationResult.Success;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    /// <summary>Hydrate from a raw settings dictionary (loaded from IAppSettingRepository).</summary>
    public static AppSettingVM FromDictionary(IReadOnlyDictionary<string, string> settings)
    {
        static string? Get(IReadOnlyDictionary<string, string> d, string key)
            => d.TryGetValue(key, out var v) ? v : null;

        var vm = new AppSettingVM
        {
            Currency = Get(settings, AppSetting.Keys.Currency) ?? "USD",
            NotificationLeadDays = int.TryParse(
                Get(settings, AppSetting.Keys.NotificationLeadDays), out var ld) ? ld : 3,
            DefaultEntryDay = int.TryParse(
                Get(settings, AppSetting.Keys.DefaultEntryDay), out var dd) ? dd : 1,
            Theme = Get(settings, AppSetting.Keys.Theme) ?? "system",
            AiApiKey = Get(settings, AppSetting.Keys.AiApiKey),
            AiProvider = Get(settings, AppSetting.Keys.AiProvider)
        };

        vm.ValidateAllProperties();
        return vm;
    }

    /// <summary>Returns key-value pairs ready to persist via IAppSettingService.</summary>
    public IEnumerable<(string Key, string Value)> ToKeyValuePairs()
    {
        yield return (AppSetting.Keys.Currency, Currency);
        yield return (AppSetting.Keys.NotificationLeadDays, NotificationLeadDays.ToString());
        yield return (AppSetting.Keys.DefaultEntryDay, DefaultEntryDay.ToString());
        yield return (AppSetting.Keys.Theme, Theme);

        if (!string.IsNullOrWhiteSpace(AiApiKey))
            yield return (AppSetting.Keys.AiApiKey, AiApiKey!);
        if (!string.IsNullOrWhiteSpace(AiProvider))
            yield return (AppSetting.Keys.AiProvider, AiProvider!);
    }
}