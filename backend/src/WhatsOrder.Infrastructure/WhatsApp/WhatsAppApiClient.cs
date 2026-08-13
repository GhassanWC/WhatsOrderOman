using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsOrder.Application.WhatsApp;

namespace WhatsOrder.Infrastructure.WhatsApp;

/// <summary>
/// Meta WhatsApp Cloud API client. All WhatsApp traffic goes through the backend —
/// tokens are never exposed to the Angular app.
/// </summary>
public class WhatsAppApiClient(
    HttpClient httpClient,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppApiClient> logger) : IWhatsAppApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly WhatsAppOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task<WhatsAppSendResult> SendTextAsync(string toE164, string body, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = ToWaNumber(toE164),
            type = "text",
            text = new { preview_url = false, body }
        };
        return await PostMessageAsync(payload, ct);
    }

    public async Task<WhatsAppSendResult> SendTemplateAsync(
        string toE164, string templateName, string languageCode,
        IReadOnlyList<string> bodyParameters, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = ToWaNumber(toE164),
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = new object[]
                {
                    new
                    {
                        type = "body",
                        parameters = bodyParameters
                            .Select(p => new { type = "text", text = p })
                            .ToArray()
                    }
                }
            }
        };
        return await PostMessageAsync(payload, ct);
    }

    private async Task<WhatsAppSendResult> PostMessageAsync(object payload, CancellationToken ct)
    {
        if (!IsConfigured)
            return WhatsAppSendResult.Fail("WhatsApp is not configured.");

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.ApiVersion}/{_options.PhoneNumberId}/messages")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            using var response = await httpClient.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = TryReadError(json) ?? $"HTTP {(int)response.StatusCode}";
                logger.LogWarning("WhatsApp send failed: {Error}", error);
                return WhatsAppSendResult.Fail(error);
            }

            using var doc = JsonDocument.Parse(json);
            var waMessageId = doc.RootElement
                .GetProperty("messages")[0]
                .GetProperty("id")
                .GetString();

            return waMessageId is null
                ? WhatsAppSendResult.Fail("Response contained no message id.")
                : WhatsAppSendResult.Ok(waMessageId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "WhatsApp send failed");
            return WhatsAppSendResult.Fail(ex.Message);
        }
    }

    private static string? TryReadError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                var code = error.TryGetProperty("code", out var c) ? c.GetRawText() : null;
                return $"{message} (code {code})";
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    /// <summary>Cloud API expects digits without the leading plus.</summary>
    private static string ToWaNumber(string e164) => e164.TrimStart('+');
}
