using FluentAssertions;
using WhatsOrder.Application.Orders;
using WhatsOrder.Domain.Enums;
using Xunit;

namespace WhatsOrder.UnitTests;

public class OrderStatusRulesTests
{
    [Theory]
    [InlineData(OrderStatus.New, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready)]
    [InlineData(OrderStatus.Ready, OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Completed)]
    [InlineData(OrderStatus.New, OrderStatus.Preparing)]     // skipping forward is allowed
    [InlineData(OrderStatus.New, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Ready, OrderStatus.Cancelled)]
    public void Allowed_transitions_for_delivery_orders(OrderStatus from, OrderStatus to) =>
        OrderStatusRules.CanTransition(from, to, FulfillmentMethod.Delivery).Should().BeTrue();

    [Theory]
    [InlineData(OrderStatus.Confirmed, OrderStatus.New)]         // backwards
    [InlineData(OrderStatus.Completed, OrderStatus.Preparing)]   // terminal
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled)]   // terminal
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]   // terminal
    [InlineData(OrderStatus.New, OrderStatus.New)]               // no-op
    public void Blocked_transitions(OrderStatus from, OrderStatus to) =>
        OrderStatusRules.CanTransition(from, to, FulfillmentMethod.Delivery).Should().BeFalse();

    [Fact]
    public void Pickup_orders_never_go_out_for_delivery() =>
        OrderStatusRules.CanTransition(OrderStatus.Ready, OrderStatus.OutForDelivery, FulfillmentMethod.Pickup)
            .Should().BeFalse();

    [Fact]
    public void Next_status_follows_the_happy_path_for_pickup()
    {
        OrderStatusRules.NextStatus(OrderStatus.New, FulfillmentMethod.Pickup).Should().Be(OrderStatus.Confirmed);
        OrderStatusRules.NextStatus(OrderStatus.Ready, FulfillmentMethod.Pickup).Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public void Next_status_includes_delivery_leg_for_delivery_orders()
    {
        OrderStatusRules.NextStatus(OrderStatus.Ready, FulfillmentMethod.Delivery).Should().Be(OrderStatus.OutForDelivery);
        OrderStatusRules.NextStatus(OrderStatus.OutForDelivery, FulfillmentMethod.Delivery).Should().Be(OrderStatus.Completed);
        OrderStatusRules.NextStatus(OrderStatus.Completed, FulfillmentMethod.Delivery).Should().BeNull();
    }
}
