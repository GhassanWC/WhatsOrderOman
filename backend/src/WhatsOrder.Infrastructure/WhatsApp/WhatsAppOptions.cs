namespace WhatsOrder.Infrastructure.WhatsApp;

/// <summary>All values come from environment variables / configuration — never from clients.</summary>
public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public bool Enabled { get; set; }
    public string ApiVersion { get; set; } = "v20.0";
    public string PhoneNumberId { get; set; } = string.Empty;
    public string BusinessAccountId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string WebhookVerifyToken { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// Business-initiated messages outside Meta's 24h customer-service window require
    /// pre-approved template messages. Free-text works with test numbers during development.
    /// </summary>
    public bool UseTemplates { get; set; }
    public string TemplateLanguage { get; set; } = "en";
    public string OrderConfirmationTemplate { get; set; } = "order_confirmation";
    public string StatusUpdateTemplate { get; set; } = "order_status_update";

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(PhoneNumberId)
        && !string.IsNullOrWhiteSpace(AccessToken);
}
