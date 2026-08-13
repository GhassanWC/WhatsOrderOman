using FluentAssertions;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Orders;
using WhatsOrder.Domain.Entities;
using Xunit;

namespace WhatsOrder.UnitTests;

public class OrderPricingTests
{
    private static Product CakeWithVariants()
    {
        var product = new Product { Name = "Cake", Price = 8.500m };
        var size = new ProductVariant { Name = "Size", NameAr = "الحجم", IsRequired = true, SortOrder = 0 };
        size.Options.Add(new ProductVariantOption { Name = "Small", PriceAdjustment = 0m, SortOrder = 0 });
        size.Options.Add(new ProductVariantOption { Name = "Large", NameAr = "كبير", PriceAdjustment = 4.500m, SortOrder = 1 });
        var flavor = new ProductVariant { Name = "Flavor", IsRequired = false, SortOrder = 1 };
        flavor.Options.Add(new ProductVariantOption { Name = "Chocolate", PriceAdjustment = 0m, SortOrder = 0 });
        flavor.Options.Add(new ProductVariantOption { Name = "Pistachio", PriceAdjustment = 1.000m, SortOrder = 1 });
        product.Variants.Add(size);
        product.Variants.Add(flavor);
        return product;
    }

    [Fact]
    public void Base_price_without_options()
    {
        var product = new Product { Name = "Cookies", Price = 3.000m };
        var (unit, original, variants) = OrderService.PriceItem(product, [], "en");
        unit.Should().Be(3.000m);
        original.Should().Be(3.000m);
        variants.Should().BeNull();
    }

    [Fact]
    public void Discounted_price_is_charged_and_discount_is_visible()
    {
        var product = new Product { Name = "Cookies", Price = 3.000m, DiscountedPrice = 2.500m };
        var (unit, original, _) = OrderService.PriceItem(product, [], "en");
        unit.Should().Be(2.500m);
        original.Should().Be(3.000m);
    }

    [Fact]
    public void Variant_adjustments_change_the_final_price()
    {
        var product = CakeWithVariants();
        var large = product.Variants.First(v => v.Name == "Size").Options.First(o => o.Name == "Large");
        var pistachio = product.Variants.First(v => v.Name == "Flavor").Options.First(o => o.Name == "Pistachio");

        var (unit, _, variantsText) = OrderService.PriceItem(product, [large.Id, pistachio.Id], "en");

        unit.Should().Be(8.500m + 4.500m + 1.000m);
        variantsText.Should().Be("Size: Large • Flavor: Pistachio");
    }

    [Fact]
    public void Arabic_variant_names_are_used_for_arabic_stores()
    {
        var product = CakeWithVariants();
        var large = product.Variants.First(v => v.Name == "Size").Options.First(o => o.Name == "Large");

        var (_, _, variantsText) = OrderService.PriceItem(product, [large.Id], "ar");

        variantsText.Should().Be("الحجم: كبير");
    }

    [Fact]
    public void Missing_required_variant_throws()
    {
        var product = CakeWithVariants();
        var act = () => OrderService.PriceItem(product, [], "en");
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("variant_required");
    }

    [Fact]
    public void Option_from_another_product_throws()
    {
        var product = CakeWithVariants();
        var act = () => OrderService.PriceItem(product, [Guid.NewGuid()], "en");
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("invalid_option");
    }

    [Fact]
    public void Two_options_from_the_same_group_throw()
    {
        var product = CakeWithVariants();
        var small = product.Variants.First(v => v.Name == "Size").Options.First(o => o.Name == "Small");
        var large = product.Variants.First(v => v.Name == "Size").Options.First(o => o.Name == "Large");

        var act = () => OrderService.PriceItem(product, [small.Id, large.Id], "en");
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("duplicate_option");
    }

    [Fact]
    public void Unavailable_option_throws()
    {
        var product = CakeWithVariants();
        var large = product.Variants.First(v => v.Name == "Size").Options.First(o => o.Name == "Large");
        large.IsAvailable = false;

        var act = () => OrderService.PriceItem(product, [large.Id], "en");
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("option_unavailable");
    }

    [Fact]
    public void Negative_adjustments_never_produce_negative_prices()
    {
        var product = new Product { Name = "Promo", Price = 1.000m };
        var variant = new ProductVariant { Name = "Deal", IsRequired = false };
        variant.Options.Add(new ProductVariantOption { Name = "Mega discount", PriceAdjustment = -5.000m });
        product.Variants.Add(variant);

        var (unit, _, _) = OrderService.PriceItem(product, [variant.Options.First().Id], "en");
        unit.Should().Be(0m);
    }
}
