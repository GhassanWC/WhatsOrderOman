using FluentAssertions;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Enums;
using Xunit;

namespace WhatsOrder.UnitTests;

public class MoneyTests
{
    [Theory]
    [InlineData(8.5, "8.500 OMR")]
    [InlineData(14.5, "14.500 OMR")]
    [InlineData(0, "0.000 OMR")]
    [InlineData(1.2345, "1.235 OMR")] // rounded to 3 places (baisa)
    public void Formats_with_three_decimals(decimal amount, string expected) =>
        Money.Format(Money.Round(amount)).Should().Be(expected);

    [Fact]
    public void Arabic_formatting_uses_rial_symbol() =>
        Money.Format(8.5m, "ar").Should().Be("8.500 ر.ع.");

    [Fact]
    public void Round_uses_away_from_zero_midpoint() =>
        Money.Round(1.0005m).Should().Be(1.001m);
}

public class PlanCatalogTests
{
    [Fact]
    public void Free_plan_has_mvp_limits()
    {
        var free = PlanCatalog.For(SubscriptionPlan.Free);
        free.MaxProducts.Should().Be(20);
        free.MaxOrdersPerMonth.Should().Be(50);
        free.WhatsAppNotifications.Should().BeFalse();
        free.Analytics.Should().BeFalse();
    }

    [Fact]
    public void Pro_plan_is_unlimited_with_whatsapp()
    {
        var pro = PlanCatalog.For(SubscriptionPlan.Pro);
        pro.MaxProducts.Should().BeNull();
        pro.MaxOrdersPerMonth.Should().BeNull();
        pro.WhatsAppNotifications.Should().BeTrue();
        pro.Analytics.Should().BeTrue();
    }
}
