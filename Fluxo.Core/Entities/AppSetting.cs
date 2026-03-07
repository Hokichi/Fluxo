using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fluxo.Core.Entities;

[Table("AppSettings")]
public class AppSetting
{
    [Key, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Well-known setting keys ────────────────────────────────────────────────
    public static class Keys
    {
        /// <summary>ISO 4217 currency code, e.g. "VND", "USD".</summary>
        public const string Currency = "currency";

        /// <summary>How many days before DueDay to fire a notification. Default "3".</summary>
        public const string NotificationLeadDays = "notification_lead_days";

        /// <summary>Day of month used as default when IsManualDate = false. Default "1".</summary>
        public const string DefaultEntryDay = "default_entry_day";

        /// <summary>Optional OpenAI / Anthropic API key for the AI insights feature.</summary>
        public const string AiApiKey = "ai_api_key";

        /// <summary>"openai" | "anthropic" — which AI provider to use.</summary>
        public const string AiProvider = "ai_provider";

        /// <summary>Theme preference: "light" | "dark" | "system".</summary>
        public const string Theme = "theme";
    }
}