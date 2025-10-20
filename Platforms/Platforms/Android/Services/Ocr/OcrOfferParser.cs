
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DidiOverlay.Platforms.Android.Services.Ocr
{
    public static class OcrOfferParser
    {
        // COP: "$ 6.500", "$6,500", "COP 6.500", etc.
        static readonly Regex RxMoney = new(
            pattern: @"(?:(?:COP|\$)\s*)?([\d\.\,]{3,})(?:\s*(?:COP|\$))?",
            options: RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // minutos: "8 min", "12mins", "10 minutos"
        static readonly Regex RxMinutes = new(
            pattern: @"\b(\d{1,2})\s*(?:min|mins|minutos?)\b",
            options: RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // km: "3.2 km", "1,8km"
        static readonly Regex RxKm = new(
            pattern: @"\b(\d{1,2}(?:[\.,]\d{1,2})?)\s*km\b",
            options: RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryParse(string text, out int fareCop, out int minutes, out double kilometers)
        {
            fareCop = 0; minutes = 0; kilometers = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            // COP → tomar el número mayor (suele ser el pago total)
            var amounts = RxMoney.Matches(text)
                .Select(m => NormalizeNumber(m.Groups[1].Value))
                .Where(v => v > 0)
                .OrderByDescending(v => v)
                .ToList();
            if (amounts.Any()) fareCop = (int)Math.Round(amounts.First());

            var mMin = RxMinutes.Match(text);
            if (mMin.Success && int.TryParse(mMin.Groups[1].Value, out var mm)) minutes = mm;

            var mKm = RxKm.Match(text);
            if (mKm.Success) kilometers = NormalizeDecimal(mKm.Groups[1].Value);

            return fareCop > 0 && minutes > 0 && kilometers > 0;
        }

        static double NormalizeNumber(string raw)
        {
            raw = raw.Trim();
            if (raw.Contains('.') && raw.Contains(','))
            {
                raw = raw.Replace(".", "").Replace(',', '.');
            }
            else if (raw.Contains('.'))
            {
                var parts = raw.Split('.');
                if (parts.Length > 1 && parts[^1].Length == 3)
                    raw = string.Join("", parts);
            }
            else if (raw.Contains(','))
            {
                var parts = raw.Split(',');
                if (parts.Length > 1 && parts[^1].Length == 3)
                    raw = string.Join("", parts);
                else
                    raw = raw.Replace(',', '.');
            }

            return double.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        static double NormalizeDecimal(string raw)
        {
            raw = raw.Replace(',', '.');
            return double.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}
