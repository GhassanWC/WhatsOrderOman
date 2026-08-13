using System.Threading.Channels;
using WhatsOrder.Application.WhatsApp;

namespace WhatsOrder.Infrastructure.WhatsApp;

/// <summary>
/// In-process bounded queue between order handling and the WhatsApp dispatcher.
/// Checkout never waits on the Meta API.
/// </summary>
public class WhatsAppNotificationQueue : IWhatsAppNotificationQueue
{
    private readonly Channel<OutboundWhatsAppNotification> _channel =
        Channel.CreateBounded<OutboundWhatsAppNotification>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelReader<OutboundWhatsAppNotification> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(OutboundWhatsAppNotification notification, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(notification, ct);
}
