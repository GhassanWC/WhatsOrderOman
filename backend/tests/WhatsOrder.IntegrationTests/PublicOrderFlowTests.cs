using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Products;
using WhatsOrder.Application.Public;
using WhatsOrder.Application.Stores;
using WhatsOrder.Application.Subscriptions;
using WhatsOrder.Domain.Enums;
using Xunit;

namespace WhatsOrder.IntegrationTests;

[Collection("api")]
public class PublicOrderFlowTests(TestAppFactory factory) : ApiTestBase(factory)
{
    private static List<VariantDto> CakeVariants() =>
    [
        new(null, "Size", "الحجم", true, 0,
        [
            new(null, "Small", null, 0m, true, 0),
            new(null, "Large", null, 4.500m, true, 1)
        ]),
        new(null, "Flavor", null, false, 1,
        [
            new(null, "Chocolate", null, 0m, true, 0),
            new(null, "Pistachio", null, 1.000m, true, 1)
        ])
    ];

    /// <summary>The core MVP flow: storefront → cart → order → owner sees it → status → WhatsApp.</summary>
    [Fact]
    public async Task Full_order_flow_end_to_end()
    {
        // Owner sets up shop (Pro plan so WhatsApp notifications are in scope).
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        await owner.PostAsJsonAsync("/api/subscription/plan", new ChangePlanRequest("Pro"), Json);
        await owner.PutAsJsonAsync("/api/store/settings", new UpdateStoreSettingsRequest(
            1.500m, 0m, true, true, store.Settings.OpeningHours, "en"), Json);

        var category = await CreateCategoryAsync(owner, "Cakes");
        var cake = await CreateProductAsync(owner, "Celebration Cake", 8.500m,
            variants: CakeVariants(), categoryId: category.Id, isFeatured: true);
        var cookies = await CreateProductAsync(owner, "Cookies Box", 3.000m,
            discountedPrice: 2.500m, stock: 10);

        // Customer browses the public storefront (no auth).
        var customer = Factory.CreateClient();
        var publicStore = await customer.GetFromJsonAsync<PublicStoreDto>(
            $"/api/public/stores/{store.Slug}", Json);
        publicStore!.AcceptingOrders.Should().BeTrue();
        publicStore.DeliveryFee.Should().Be(1.500m);
        publicStore.Categories.Should().ContainSingle(c => c.Name == "Cakes");

        var products = await customer.GetFromJsonAsync<List<PublicProductDto>>(
            $"/api/public/stores/{store.Slug}/products", Json);
        products!.Should().HaveCount(2);

        var publicCake = products.Single(p => p.Name == "Celebration Cake");
        var large = publicCake.Variants.Single(v => v.Name == "Size").Options.Single(o => o.Name == "Large");
        var pistachio = publicCake.Variants.Single(v => v.Name == "Flavor").Options.Single(o => o.Name == "Pistachio");

        // Customer checks out: 1× Large Pistachio cake + 2× discounted cookies, delivered.
        var orderResponse = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest(
                "Ahmed", "9234 5678", FulfillmentMethod.Delivery,
                "Al Khuwair, Muscat", "https://maps.google.com/?q=23.5,58.4", "Today 6pm", "Ring the bell",
                [
                    new PublicOrderItemRequest(publicCake.Id, 1, [large.Id, pistachio.Id]),
                    new PublicOrderItemRequest(cookies.Id, 2, null)
                ]), Json);
        orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = (await orderResponse.Content.ReadFromJsonAsync<PublicOrderCreatedDto>(Json))!;

        // Server-side pricing: cake 8.500+4.500+1.000 = 14.000; cookies 2×2.500 = 5.000.
        created.OrderNumber.Should().Be("WO-1001");
        created.Subtotal.Should().Be(19.000m);
        created.DeliveryFee.Should().Be(1.500m);
        created.Discount.Should().Be(1.000m); // 2 × (3.000 − 2.500)
        created.Total.Should().Be(20.500m);

        // Stock was decremented.
        var cookiesAfter = await owner.GetFromJsonAsync<ProductDto>($"/api/products/{cookies.Id}", Json);
        cookiesAfter!.StockQuantity.Should().Be(8);

        // Owner sees the new order with full details.
        var orders = await owner.GetFromJsonAsync<OrdersPage>("/api/orders?filter=new", Json);
        orders!.Items.Should().ContainSingle();
        var listItem = orders.Items.Single();
        listItem.CustomerPhone.Should().Be("+96892345678");
        listItem.Total.Should().Be(20.500m);

        var detail = await owner.GetFromJsonAsync<OrderDto>($"/api/orders/{listItem.Id}", Json);
        detail!.Items.Should().HaveCount(2);
        detail.Items.Single(i => i.ProductName == "Celebration Cake")
            .VariantsText.Should().Be("Size: Large • Flavor: Pistachio");
        detail.Notes.Should().Be("Ring the bell");

        // WhatsApp: confirmation to the customer + alert to the owner were dispatched.
        var processed = await Factory.WaitForAsync<List<WhatsOrder.Domain.Entities.WhatsAppMessage>>(async () =>
        {
            List<WhatsOrder.Domain.Entities.WhatsAppMessage>? rows = null;
            await Factory.WithDbAsync(async db =>
            {
                var r = await db.WhatsAppMessages
                    .Where(m => m.Order!.OrderNumber == "WO-1001"
                                && m.Store!.Slug == store.Slug   // order numbers repeat across stores
                                && m.Status != WhatsAppMessageStatus.Pending)
                    .ToListAsync();
                rows = r.Count >= 2 ? r : null;
            });
            return rows;
        });
        processed.Should().OnlyContain(m => m.Status == WhatsAppMessageStatus.Sent,
            "the Pro plan includes WhatsApp notifications, so nothing should be skipped or failed. " +
            $"Actual: {string.Join(" | ", processed.Select(m => $"{m.Type}:{m.Status}:{m.Error}"))}");

        var sentMessages = Factory.WhatsAppClient.Messages;
        // Customer confirmation (customer phone 92345678).
        sentMessages.Should().Contain(m => m.To == "+96892345678"
            && m.Body.Contains("Hello Ahmed")
            && m.Body.Contains("#WO-1001")
            && m.Body.Contains("Total: 20.500 OMR"));
        // Owner alert (store WhatsApp number 91234567).
        sentMessages.Should().Contain(m => m.To == "+96891234567"
            && m.Body.Contains("New order #WO-1001"));

        // Owner confirms the order → customer gets a status update.
        var confirm = await owner.PatchAsJsonAsync($"/api/orders/{listItem.Id}/status",
            new UpdateOrderStatusRequest(OrderStatus.Confirmed), Json);
        confirm.EnsureSuccessStatusCode();

        await Factory.WaitForAsync(async () =>
            Factory.WhatsAppClient.Messages.Any(m => m.Body.Contains("has been confirmed"))
                ? new object() : null);

        // Customer can check their order status with their phone number.
        var status = await customer.GetFromJsonAsync<PublicOrderStatusDto>(
            $"/api/public/stores/{store.Slug}/orders/{created.OrderNumber}?phone=92345678", Json);
        status!.Status.Should().Be(OrderStatus.Confirmed);

        // A wrong phone number reveals nothing.
        (await customer.GetAsync($"/api/public/stores/{store.Slug}/orders/{created.OrderNumber}?phone=99999999"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // All messages were recorded in the message log as Sent.
        await Factory.WithDbAsync(async db =>
        {
            var logged = await db.WhatsAppMessages
                .Where(m => m.Order!.OrderNumber == "WO-1001" && m.Store!.Slug == store.Slug)
                .ToListAsync();
            logged.Should().HaveCountGreaterThanOrEqualTo(3);
            logged.Should().OnlyContain(m => m.Status == WhatsAppMessageStatus.Sent);
        });
    }

    [Fact]
    public async Task Store_closed_for_orders_rejects_checkout()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        await owner.PutAsJsonAsync("/api/store", new UpdateStoreRequest(
            store.Name, store.NameAr, store.Slug, store.WhatsAppNumber, store.InstagramHandle,
            store.Description, store.DescriptionAr, store.LocationText, store.Governorate,
            store.Wilayat, IsAcceptingOrders: false), Json);

        var customer = Factory.CreateClient();
        var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 1, null)]), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("store_closed");
    }

    [Fact]
    public async Task Below_minimum_order_is_rejected()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, price: 2.000m);
        await owner.PutAsJsonAsync("/api/store/settings", new UpdateStoreSettingsRequest(
            0m, 5.000m, true, true, store.Settings.OpeningHours, "en"), Json);

        var customer = Factory.CreateClient();
        var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 1, null)]), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("min_order");
    }

    [Fact]
    public async Task Delivery_without_address_fails_validation()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        var customer = Factory.CreateClient();
        var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ali", "91234567", FulfillmentMethod.Delivery, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 1, null)]), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ordering_more_than_stock_is_rejected()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, stock: 2);

        var customer = Factory.CreateClient();
        var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 3, null)]), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("insufficient_stock");
    }

    [Fact]
    public async Task Missing_required_variant_is_rejected()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var cake = await CreateProductAsync(owner, "Cake", 8.500m, variants: CakeVariants());

        var customer = Factory.CreateClient();
        var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(cake.Id, 1, null)]), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("variant_required");
    }

    [Fact]
    public async Task Cancelling_an_order_restocks_tracked_products()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, stock: 5);

        var customer = Factory.CreateClient();
        var orderResponse = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 3, null)]), Json);
        orderResponse.EnsureSuccessStatusCode();

        (await owner.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}", Json))!
            .StockQuantity.Should().Be(2);

        var orders = await owner.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var orderId = orders!.Items.First().Id;
        var cancel = await owner.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatus.Cancelled), Json);
        cancel.EnsureSuccessStatusCode();

        (await owner.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}", Json))!
            .StockQuantity.Should().Be(5);
    }

    [Fact]
    public async Task Completed_orders_cannot_change_status()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        var customer = Factory.CreateClient();
        await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 1, null)]), Json);

        var orders = await owner.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var orderId = orders!.Items.First().Id;

        (await owner.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatus.Completed), Json)).EnsureSuccessStatusCode();

        var reopen = await owner.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatus.Preparing), Json);
        reopen.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await reopen.Content.ReadAsStringAsync()).Should().Contain("invalid_transition");
    }

    [Fact]
    public async Task Order_numbers_increment_per_store()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        var customer = Factory.CreateClient();
        for (var i = 1; i <= 3; i++)
        {
            var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
                new CreatePublicOrderRequest("Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                    [new PublicOrderItemRequest(product.Id, 1, null)]), Json);
            var created = (await response.Content.ReadFromJsonAsync<PublicOrderCreatedDto>(Json))!;
            created.OrderNumber.Should().Be($"WO-{1000 + i}");
        }
    }
}
