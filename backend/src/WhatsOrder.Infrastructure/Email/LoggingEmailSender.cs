using Microsoft.Extensions.Logging;
using WhatsOrder.Application.Common;

namespace WhatsOrder.Infrastructure.Email;

/// <summary>
/// MVP email sender: writes the email to the application log (so password-reset links
/// are usable in development). Replace with an SMTP/SendGrid/SES implementation of
/// IEmailSender for production email delivery.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("EMAIL to {To} — {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
