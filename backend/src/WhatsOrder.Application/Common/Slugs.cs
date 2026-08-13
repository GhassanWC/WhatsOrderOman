using System.Text.RegularExpressions;

namespace WhatsOrder.Application.Common;

public static partial class Slugs
{
    public const int MinLength = 3;
    public const int MaxLength = 40;

    /// <summary>Paths owned by the app itself — can never be store slugs.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "admin", "app", "www", "assets", "uploads", "swagger",
        "dashboard", "login", "register", "logout", "onboarding",
        "forgot-password", "reset-password", "account", "settings",
        "store", "stores", "order", "orders", "products", "cart", "checkout",
        "pricing", "terms", "privacy", "about", "contact", "help", "support",
        "whatsorder", "static", "public", "media",
        "search", "offers", "favorites", "messages", "business", "explore",
        "notifications", "profile", "addresses", "payments", "billing", "reviews"
    };

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$")]
    private static partial Regex Pattern();

    public static string Normalize(string? input) => (input ?? "").Trim().ToLowerInvariant();

    public static bool IsValidFormat(string slug) =>
        slug.Length is >= MinLength and <= MaxLength &&
        Pattern().IsMatch(slug) &&
        !slug.Contains("--");

    public static bool IsReserved(string slug) => Reserved.Contains(slug);

    public static bool IsValid(string? input)
    {
        var slug = Normalize(input);
        return IsValidFormat(slug) && !IsReserved(slug);
    }
}
