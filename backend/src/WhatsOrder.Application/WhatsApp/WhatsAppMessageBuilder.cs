using System.Text;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.WhatsApp;

/// <summary>
/// Builds the WhatsApp message texts (English or Arabic, per store default language).
/// Pure functions — unit tested.
/// </summary>
public static class WhatsAppMessageBuilder
{
    public static string BuildOrderConfirmation(Store store, Order order, string lang)
    {
        var storeName = DisplayStoreName(store, lang);
        var sb = new StringBuilder();

        if (lang == "ar")
        {
            sb.AppendLine($"مرحباً {order.CustomerName} 👋");
            sb.AppendLine();
            sb.AppendLine($"تم استلام طلبك #{order.OrderNumber} من {storeName}.");
            sb.AppendLine();
            sb.AppendLine("الطلب:");
            AppendItems(sb, order, lang);
            sb.AppendLine();
            if (order.DeliveryFee > 0)
                sb.AppendLine($"رسوم التوصيل: {Money.Format(order.DeliveryFee, lang)}");
            sb.AppendLine($"الإجمالي: {Money.Format(order.Total, lang)}");
            sb.AppendLine();
            sb.AppendLine($"حالة الطلب: {StatusText(order.Status, lang)}");
            sb.AppendLine();
            sb.Append("سنقوم بإشعارك عندما يؤكد المتجر طلبك.");
        }
        else
        {
            sb.AppendLine($"Hello {order.CustomerName} 👋");
            sb.AppendLine();
            sb.AppendLine($"Your order #{order.OrderNumber} has been received by {storeName}.");
            sb.AppendLine();
            sb.AppendLine("Items:");
            AppendItems(sb, order, lang);
            sb.AppendLine();
            if (order.DeliveryFee > 0)
                sb.AppendLine($"Delivery fee: {Money.Format(order.DeliveryFee, lang)}");
            sb.AppendLine($"Total: {Money.Format(order.Total, lang)}");
            sb.AppendLine();
            sb.AppendLine($"Order Status: {StatusText(order.Status, lang)}");
            sb.AppendLine();
            sb.Append("We will notify you when the store confirms your order.");
        }

        return sb.ToString();
    }

    public static string BuildStatusUpdate(Store store, Order order, string lang)
    {
        var storeName = DisplayStoreName(store, lang);

        if (lang == "ar")
        {
            var line = order.Status switch
            {
                OrderStatus.Confirmed => $"تم تأكيد طلبك #{order.OrderNumber} من {storeName} ✅",
                OrderStatus.Preparing => $"طلبك #{order.OrderNumber} قيد التحضير الآن 👨‍🍳",
                OrderStatus.Ready => order.FulfillmentMethod == FulfillmentMethod.Pickup
                    ? $"طلبك #{order.OrderNumber} جاهز للاستلام 🛍️"
                    : $"طلبك #{order.OrderNumber} جاهز 🛍️",
                OrderStatus.OutForDelivery => $"طلبك #{order.OrderNumber} في الطريق إليك 🛵",
                OrderStatus.Completed => $"اكتمل طلبك #{order.OrderNumber}. شكراً لتسوقك من {storeName} 💚",
                OrderStatus.Cancelled => $"نأسف، تم إلغاء طلبك #{order.OrderNumber} من {storeName}.",
                OrderStatus.Rejected => $"نأسف، لم يتمكن {storeName} من قبول طلبك #{order.OrderNumber}.",
                _ => $"تحديث حالة طلبك #{order.OrderNumber}: {StatusText(order.Status, lang)}"
            };
            return line;
        }

        return order.Status switch
        {
            OrderStatus.Confirmed => $"Your order #{order.OrderNumber} has been confirmed by {storeName} ✅",
            OrderStatus.Preparing => $"Your order #{order.OrderNumber} is now being prepared 👨‍🍳",
            OrderStatus.Ready => order.FulfillmentMethod == FulfillmentMethod.Pickup
                ? $"Your order #{order.OrderNumber} is ready for pickup 🛍️"
                : $"Your order #{order.OrderNumber} is ready 🛍️",
            OrderStatus.OutForDelivery => $"Your order #{order.OrderNumber} is on its way 🛵",
            OrderStatus.Completed => $"Your order #{order.OrderNumber} is completed. Thank you for ordering from {storeName} 💚",
            OrderStatus.Cancelled => $"We're sorry — your order #{order.OrderNumber} was cancelled by {storeName}.",
            OrderStatus.Rejected => $"We're sorry — {storeName} could not accept your order #{order.OrderNumber}.",
            _ => $"Your order #{order.OrderNumber} status: {StatusText(order.Status, lang)}"
        };
    }

    public static string BuildOwnerNewOrderAlert(Store store, Order order, string lang)
    {
        var sb = new StringBuilder();

        if (lang == "ar")
        {
            sb.AppendLine($"🛎️ طلب جديد #{order.OrderNumber}");
            sb.AppendLine();
            sb.AppendLine($"العميل: {order.CustomerName}");
            sb.AppendLine($"الهاتف: {order.CustomerPhone}");
            sb.AppendLine(order.FulfillmentMethod == FulfillmentMethod.Delivery
                ? $"توصيل: {order.DeliveryAddress}"
                : "استلام من المتجر");
            if (!string.IsNullOrWhiteSpace(order.PreferredTime))
                sb.AppendLine($"الوقت المفضل: {order.PreferredTime}");
            sb.AppendLine();
            AppendItems(sb, order, lang);
            if (!string.IsNullOrWhiteSpace(order.Notes))
            {
                sb.AppendLine();
                sb.AppendLine($"ملاحظات: {order.Notes}");
            }
            sb.AppendLine();
            sb.Append($"الإجمالي: {Money.Format(order.Total, lang)}");
        }
        else
        {
            sb.AppendLine($"🛎️ New order #{order.OrderNumber}");
            sb.AppendLine();
            sb.AppendLine($"Customer: {order.CustomerName}");
            sb.AppendLine($"Phone: {order.CustomerPhone}");
            sb.AppendLine(order.FulfillmentMethod == FulfillmentMethod.Delivery
                ? $"Delivery: {order.DeliveryAddress}"
                : "Pickup at store");
            if (!string.IsNullOrWhiteSpace(order.PreferredTime))
                sb.AppendLine($"Preferred time: {order.PreferredTime}");
            sb.AppendLine();
            AppendItems(sb, order, lang);
            if (!string.IsNullOrWhiteSpace(order.Notes))
            {
                sb.AppendLine();
                sb.AppendLine($"Notes: {order.Notes}");
            }
            sb.AppendLine();
            sb.Append($"Total: {Money.Format(order.Total, lang)}");
        }

        return sb.ToString();
    }

    public static string StatusText(OrderStatus status, string lang) => lang == "ar"
        ? status switch
        {
            OrderStatus.New => "جديد",
            OrderStatus.Confirmed => "مؤكد",
            OrderStatus.Preparing => "قيد التحضير",
            OrderStatus.Ready => "جاهز",
            OrderStatus.OutForDelivery => "في الطريق",
            OrderStatus.Completed => "مكتمل",
            OrderStatus.Cancelled => "ملغي",
            OrderStatus.Rejected => "مرفوض",
            _ => status.ToString()
        }
        : status switch
        {
            OrderStatus.New => "New",
            OrderStatus.Confirmed => "Confirmed",
            OrderStatus.Preparing => "Preparing",
            OrderStatus.Ready => "Ready",
            OrderStatus.OutForDelivery => "Out for Delivery",
            OrderStatus.Completed => "Completed",
            OrderStatus.Cancelled => "Cancelled",
            OrderStatus.Rejected => "Rejected",
            _ => status.ToString()
        };

    private static void AppendItems(StringBuilder sb, Order order, string lang)
    {
        foreach (var item in order.Items)
        {
            var variants = string.IsNullOrWhiteSpace(item.VariantsText) ? "" : $" ({item.VariantsText})";
            sb.AppendLine($"{item.Quantity}x {item.ProductName}{variants}");
        }
    }

    private static string DisplayStoreName(Store store, string lang) =>
        lang == "ar" && !string.IsNullOrWhiteSpace(store.NameAr) ? store.NameAr! : store.Name;
}
