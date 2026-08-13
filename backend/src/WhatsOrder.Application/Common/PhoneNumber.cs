using System.Text.RegularExpressions;

namespace WhatsOrder.Application.Common;

/// <summary>
/// Phone normalization to E.164. Oman-first (bare 8-digit numbers get +968),
/// but any valid international number is accepted so expansion isn't blocked.
/// </summary>
public static partial class PhoneNumber
{
    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex E164();

    [GeneratedRegex(@"[\s\-\.\(\)]")]
    private static partial Regex Separators();

    /// <summary>Returns the normalized E.164 number (+968XXXXXXXX) or null when invalid.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var value = Separators().Replace(input.Trim(), "");

        if (value.StartsWith("00"))
            value = "+" + value[2..];

        if (!value.StartsWith('+'))
        {
            if (value.Length == 8 && value.All(char.IsAsciiDigit))
                value = "+968" + value;                       // bare local Omani number
            else if (value.Length == 11 && value.StartsWith("968") && value.All(char.IsAsciiDigit))
                value = "+" + value;                          // 968XXXXXXXX without plus
            else if (value.Length == 9 && value.StartsWith('0') && value[1..].All(char.IsAsciiDigit))
                value = "+968" + value[1..];                  // 0XXXXXXXX trunk-style
            else
                return null;
        }

        if (!E164().IsMatch(value))
            return null;

        // Omani numbers: exactly 8 national digits starting with 9/7 (mobile) or 2 (landline).
        if (value.StartsWith("+968"))
        {
            var national = value[4..];
            if (national.Length != 8 || national[0] is not ('9' or '7' or '2'))
                return null;
        }

        return value;
    }

    public static bool IsValid(string? input) => Normalize(input) is not null;
}
