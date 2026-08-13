using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.WhatsApp;

/// <summary>A message ready to send, queued for the background dispatcher.</summary>
public sealed record OutboundWhatsAppNotification(
    Guid StoreId,
    Guid OrderId,
    WhatsAppMessageType Type,
    string Phone,
    string Body);

public interface IWhatsAppNotificationQueue
{
    ValueTask EnqueueAsync(OutboundWhatsAppNotification notification, CancellationToken ct = default);
}

public sealed record WhatsAppSendResult(bool Success, string? WaMessageId, string? Error)
{
    public static WhatsAppSendResult Ok(string waMessageId) => new(true, waMessageId, null);
    public static WhatsAppSendResult Fail(string error) => new(false, null, error);
}

/// <summary>Low-level Meta WhatsApp Cloud API client (implemented in Infrastructure).</summary>
public interface IWhatsAppApiClient
{
    bool IsConfigured { get; }

    Task<WhatsAppSendResult> SendTextAsync(string toE164, string body, CancellationToken ct = default);

    /// <summary>For business-initiated messages outside Meta's 24h customer-service window.</summary>
    Task<WhatsAppSendResult> SendTemplateAsync(
        string toE164, string templateName, string languageCode,
        IReadOnlyList<string> bodyParameters, CancellationToken ct = default);
}
