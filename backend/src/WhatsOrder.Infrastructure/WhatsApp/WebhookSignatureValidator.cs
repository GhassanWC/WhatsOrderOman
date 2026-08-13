using System.Security.Cryptography;
using System.Text;

namespace WhatsOrder.Infrastructure.WhatsApp;

public static class WebhookSignatureValidator
{
    /// <summary>
    /// Meta signs webhook payloads with HMAC-SHA256 over the raw body using the app secret,
    /// sent as "X-Hub-Signature-256: sha256=&lt;hex&gt;". Constant-time comparison.
    /// </summary>
    public static bool IsValid(string appSecret, byte[] rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var providedHex = signatureHeader[prefix.Length..].Trim();
        byte[] provided;
        try
        {
            provided = Convert.FromHexString(providedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), rawBody);
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
