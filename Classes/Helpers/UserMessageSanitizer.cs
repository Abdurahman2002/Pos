using System.Globalization;
using System.Text.RegularExpressions;

namespace NewsApp2.Classes.Helpers
{
    public static class UserMessageSanitizer
    {
        private static readonly Regex GuidRegex = new(
            "\\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\b",
            RegexOptions.Compiled);

        private static readonly Regex DecimalRegex = new(
            "\\b\\d+\\.\\d+\\b",
            RegexOptions.Compiled);

        public static string Sanitize(string? message, string fallback)
        {
            if (string.IsNullOrWhiteSpace(message))
                return fallback;

            var text = message.Trim();
            text = text.Replace("\r", " ").Replace("\n", " ");
            text = GuidRegex.Replace(text, "item");

            text = DecimalRegex.Replace(text, m =>
            {
                if (decimal.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    return value.ToString("0.##", CultureInfo.InvariantCulture);

                return m.Value;
            });

            if (text.Length > 300)
                return fallback;

            return text;
        }
    }
}