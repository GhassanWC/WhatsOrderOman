using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Products;
using WhatsOrder.Application.Subscriptions;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;
using Xunit;

namespace WhatsOrder.IntegrationTests;

[Collection("api")]
public class PlanLimitTests(TestAppFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Free_plan_caps_products_at_20_and_pro_lifts_the_cap()
    {
        var (owner, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(owner);

        for (var i = 1; i <= 20; i++)
            await CreateProductAsync(owner, $"Product {i}");

        var over = await owner.PostAsJsonAsync("/api/products", new SaveProductRequest(
            null, "Product 21", null, null, null, 1.000m, null, null, true, false, null), Json);
        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await over.Content.ReadAsStringAsync()).Should().Contain("plan_limit_products");

        // Upgrade → unlimited.
        var upgrade = await owner.PostAsJsonAsync("/api/subscription/plan", new ChangePlanRequest("Pro"), Json);
        upgrade.EnsureSuccessStatusCode();
        await CreateProductAsync(owner, "Product 21");

        var subscription = await owner.GetFromJsonAsync<SubscriptionDto>("/api/subscription", Json);
        subscription!.Plan.Should().Be("Pro");
        subscription.ProductsUsed.Should().Be(21);
    }

    [Fact]
    public async Task Free_plan_monthly_order_limit_blocks_checkout_and_hides_ordering()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        // Simulate 50 orders this month directly in the database.
        await Factory.WithDbAsync(async db =>
        {
            var storeId = (await db.Stores.SingleAsync(s => s.Slug == store.Slug)).Id;
            for (var i = 0; i < 50; i++)
            {
                db.Orders.Add(new Order
                {
                    StoreId = storeId,
                    OrderNumber = $"WO-{9000 + i}",
                    CustomerName = "Bulk",
                    CustomerPhone = "+96899999999",
                    Status = OrderStatus.Completed,
                    FulfillmentMethod = FulfillmentMethod.Pickup,
                    Subtotal = 1,
                    Total = 1
                });
            }
            await db.SaveChangesAsync();
        });

        var customer = Factory.CreateClient();
        var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new WhatsOrder.Application.Orders.CreatePublicOrderRequest(
                "Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                [new WhatsOrder.Application.Orders.PublicOrderItemRequest(product.Id, 1, null)]), Json);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("store_order_limit");

        var publicStore = await customer.GetFromJsonAsync<WhatsOrder.Application.Public.PublicStoreDto>(
            $"/api/public/stores/{store.Slug}", Json);
        publicStore!.AcceptingOrders.Should().BeFalse();
    }

    [Fact]
    public async Task Free_plan_skips_whatsapp_notifications_but_logs_them()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner); // Free plan by default
        var product = await CreateProductAsync(owner, $"Unique-{Guid.NewGuid():N}"[..20]);

        var customer = Factory.CreateClient();
        var orderResponse = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new WhatsOrder.Application.Orders.CreatePublicOrderRequest(
                "Ali", "91234567", FulfillmentMethod.Pickup, null, null, null, null,
                [new WhatsOrder.Application.Orders.PublicOrderItemRequest(product.Id, 1, null)]), Json);
        orderResponse.EnsureSuccessStatusCode();

        await Factory.WaitForAsync(async () =>
        {
            WhatsAppMessage? found = null;
            await Factory.WithDbAsync(async db =>
            {
                found = await db.WhatsAppMessages
                    .Where(m => m.StoreId != null && m.Store!.Slug == store.Slug)
                    .FirstOrDefaultAsync(m => m.Status == WhatsAppMessageStatus.Skipped);
            });
            return found;
        });
    }
}

[Collection("api")]
public class WebhookTests(TestAppFactory factory) : ApiTestBase(factory)
{
    private static StringContent Signed(string body)
    {
        var signature = "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(TestAppFactory.WebhookAppSecret),
                Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);
        return content;
    }

    [Fact]
    public async Task Verification_handshake_echoes_the_challenge_only_with_the_right_token()
    {
        var client = Factory.CreateClient();

        var ok = await client.GetAsync(
            $"/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token={TestAppFactory.WebhookVerifyToken}&hub.challenge=12345");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ok.Content.ReadAsStringAsync()).Should().Be("12345");

        var bad = await client.GetAsync(
            "/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=12345");
        bad.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unsigned_webhook_posts_are_rejected()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsync("/api/webhooks/whatsapp",
            new StringContent("""{"entry":[]}""", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signed_status_update_advances_the_message_log()
    {
        var waMessageId = $"wamid.hook.{Guid.NewGuid():N}";
        await Factory.WithDbAsync(async db =>
        {
            db.WhatsAppMessages.Add(new WhatsAppMessage
            {
                Direction = WhatsAppDirection.Outbound,
                Type = WhatsAppMessageType.OrderConfirmation,
                Phone = "+96891234567",
                Body = "test",
                WaMessageId = waMessageId,
                Status = WhatsAppMessageStatus.Sent
            });
            await db.SaveChangesAsync();
        });

        var payload = $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "0",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "statuses": [{ "id": "{{waMessageId}}", "status": "delivered", "timestamp": "0" }]
              }
            }]
          }]
        }
        """;

        var client = Factory.CreateClient();
        var response = await client.PostAsync("/api/webhooks/whatsapp", Signed(payload));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.WithDbAsync(async db =>
        {
            var message = await db.WhatsAppMessages.SingleAsync(m => m.WaMessageId == waMessageId);
            message.Status.Should().Be(WhatsAppMessageStatus.Delivered);
        });
    }

    [Fact]
    public async Task Signed_inbound_message_is_persisted()
    {
        var inboundId = $"wamid.inbound.{Guid.NewGuid():N}";
        var payload = $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "0",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "messages": [{
                  "from": "96891234567",
                  "id": "{{inboundId}}",
                  "type": "text",
                  "text": { "body": "Is my order ready?" }
                }]
              }
            }]
          }]
        }
        """;

        var client = Factory.CreateClient();
        var response = await client.PostAsync("/api/webhooks/whatsapp", Signed(payload));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.WithDbAsync(async db =>
        {
            var message = await db.WhatsAppMessages.SingleAsync(m => m.WaMessageId == inboundId);
            message.Direction.Should().Be(WhatsAppDirection.Inbound);
            message.Body.Should().Be("Is my order ready?");
            message.Phone.Should().Be("+96891234567");
        });
    }
}
