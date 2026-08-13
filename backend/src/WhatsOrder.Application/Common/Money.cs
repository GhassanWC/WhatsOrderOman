using System.Globalization;

namespace WhatsOrder.Application.Common;

/// <summary>OMR uses 3 decimal places (1 rial = 1000 baisa).</summary>
public static class Money
{
    public static string Format(decimal amount, string lang = "en")
    {
        var number = amount.ToString("0.000", CultureInfo.InvariantCulture);
        return lang == "ar" ? $"{number} ر.ع." : $"{number} OMR";
    }

    public static decimal Round(decimal amount) => Math.Round(amount, 3, MidpointRounding.AwayFromZero);
}
