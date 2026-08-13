using FluentAssertions;
using WhatsOrder.Application.WhatsApp;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;
using Xunit;

namespace WhatsOrder.UnitTests;

public class WhatsAppMessageBuilderTests
{
    private static (Store Store, Order Order) SampleOrder()
    {
        var store = new Store { Name = "Al Reem Cakes", NameAr = "حلويات الريم", WhatsAppNumber = "+96890000001" };
        var order = new Order
        {
            OrderNumber = "WO-1024",
            CustomerName = "Ahmed",
            CustomerPhone = "+96891234567",
            Status = OrderStatus.New,
            FulfillmentMethod = FulfillmentMethod.Delivery,
            DeliveryAddress = "Al Khuwair, Muscat",
            Subtotal = 13.000m,
            DeliveryFee = 1.500m,
            Total = 14.500m
        };
        order.Items.Add(new OrderItem { ProductName = "Chocolate Cake", Quantity = 2, UnitPrice = 5.000m, LineTotal = 10.000m });
        order.Items.Add(new OrderItem { ProductName = "Cookies Box", Quantity = 1, UnitPrice = 3.000m, LineTotal = 3.000m, VariantsText = "Size: Small" });
        return (store, order);
    }

    [Fact]
    public void English_confirmation_contains_all_key_details()
    {
        var (store, order) = SampleOrder();
        var text = WhatsAppMessageBuilder.BuildOrderConfirmation(store, order, "en");

        text.Should().Contain("Hello Ahmed 👋");
        text.Should().Contain("#WO-1024");
        text.Should().Contain("Al Reem Cakes");
        text.Should().Contain("2x Chocolate Cake");
        text.Should().Contain("1x Cookies Box (Size: Small)");
        text.Should().Contain("Total: 14.500 OMR");
        text.Should().Contain("Order Status: New");
    }

    [Fact]
    public void Arabic_confirmation_uses_arabic_store_name_and_currency()
    {
        var (store, order) = SampleOrder();
        var text = WhatsAppMessageBuilder.BuildOrderConfirmation(store, order, "ar");

        text.Should().Contain("مرحباً Ahmed");
        text.Should().Contain("حلويات الريم");
        text.Should().Contain("14.500 ر.ع.");
        text.Should().Contain("#WO-1024");
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed, "confirmed")]
    [InlineData(OrderStatus.Preparing, "being prepared")]
    [InlineData(OrderStatus.OutForDelivery, "on its way")]
    [InlineData(OrderStatus.Completed, "completed")]
    [InlineData(OrderStatus.Cancelled, "cancelled")]
    public void Status_updates_describe_the_new_status(OrderStatus status, string phrase)
    {
        var (store, order) = SampleOrder();
        order.Status = status;
        var text = WhatsAppMessageBuilder.BuildStatusUpdate(store, order, "en");

        text.Should().Contain("#WO-1024");
        text.ToLowerInvariant().Should().Contain(phrase);
    }

    [Fact]
    public void Ready_pickup_orders_mention_pickup()
    {
        var (store, order) = SampleOrder();
        order.FulfillmentMethod = FulfillmentMethod.Pickup;
        order.Status = OrderStatus.Ready;

        WhatsAppMessageBuilder.BuildStatusUpdate(store, order, "en")
            .Should().Contain("ready for pickup");
    }

    [Fact]
    public void Owner_alert_contains_customer_and_fulfillment_details()
    {
        var (store, order) = SampleOrder();
        order.Notes = "No nuts please";
        var text = WhatsAppMessageBuilder.BuildOwnerNewOrderAlert(store, order, "en");

        text.Should().Contain("New order #WO-1024");
        text.Should().Contain("Ahmed");
        text.Should().Contain("+96891234567");
        text.Should().Contain("Delivery: Al Khuwair, Muscat");
        text.Should().Contain("No nuts please");
        text.Should().Contain("Total: 14.500 OMR");
    }
}
