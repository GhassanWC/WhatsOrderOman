using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Stores;
using WhatsOrder.Domain.Enums;
using Xunit;

namespace WhatsOrder.IntegrationTests;

[Collection("api")]
public class StoreTests(TestAppFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_store_and_read_it_back()
    {
        var (client, _) = await RegisterOwnerAsync();
        var created = await CreateStoreAsync(client);

        created.WhatsAppNumber.Should().Be("+96891234567"); // normalized
        created.Plan.Should().Be("Free");
        created.Settings.OpeningHours.Should().HaveCount(7);

        var fetched = await client.GetFromJsonAsync<StoreDto>("/api/store", Json);
        fetched!.Slug.Should().Be(created.Slug);
    }

    [Fact]
    public async Task Second_store_for_the_same_owner_conflicts()
    {
        var (client, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(client);

        var response = await client.PostAsJsonAsync("/api/store", new CreateStoreRequest(
            "Another", null, UniqueSlug(), "91234567", null, null, null, null, null, null), Json);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Taken_slug_conflicts_and_reserved_slug_is_rejected()
    {
        var (clientA, _) = await RegisterOwnerAsync();
        var slug = UniqueSlug();
        await CreateStoreAsync(clientA, slug);

        var (clientB, _) = await RegisterOwnerAsync();
        var taken = await clientB.PostAsJsonAsync("/api/store", new CreateStoreRequest(
            "B Store", null, slug, "91234567", null, null, null, null, null, null), Json);
        taken.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var reserved = await clientB.PostAsJsonAsync("/api/store", new CreateStoreRequest(
            "B Store", null, "dashboard", "91234567", null, null, null, null, null, null), Json);
        reserved.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Slug_availability_endpoint_reports_correctly()
    {
        var (client, _) = await RegisterOwnerAsync();
        var slug = UniqueSlug();
        await CreateStoreAsync(client, slug);

        var mine = await client.GetFromJsonAsync<SlugAvailabilityDto>($"/api/store/slug-available?slug={slug}", Json);
        mine!.IsAvailable.Should().BeTrue("a store's own slug counts as available to itself");

        var (other, _) = await RegisterOwnerAsync();
        var otherView = await other.GetFromJsonAsync<SlugAvailabilityDto>($"/api/store/slug-available?slug={slug}", Json);
        otherView!.IsAvailable.Should().BeFalse();

        var reserved = await client.GetFromJsonAsync<SlugAvailabilityDto>("/api/store/slug-available?slug=api", Json);
        reserved!.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Settings_update_round_trips()
    {
        var (client, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(client);

        var hours = store.Settings.OpeningHours
            .Select(h => h.Day == 5 ? h with { Closed = true } : h).ToList();

        var response = await client.PutAsJsonAsync("/api/store/settings", new UpdateStoreSettingsRequest(
            DeliveryFee: 1.500m,
            MinimumOrderAmount: 5.000m,
            DeliveryEnabled: true,
            PickupEnabled: true,
            OpeningHours: hours,
            DefaultLanguage: "ar"), Json);
        response.EnsureSuccessStatusCode();

        var updated = (await response.Content.ReadFromJsonAsync<StoreDto>(Json))!;
        updated.Settings.DeliveryFee.Should().Be(1.500m);
        updated.Settings.OpeningHours.Single(h => h.Day == 5).Closed.Should().BeTrue();
        updated.Settings.DefaultLanguage.Should().Be("ar");
    }
}

[Collection("api")]
public class TenantIsolationTests(TestAppFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Owners_cannot_see_or_touch_another_stores_data()
    {
        var (clientA, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(clientA);
        var productA = await CreateProductAsync(clientA, "Secret Product A");
        var categoryA = await CreateCategoryAsync(clientA, "Category A");

        var (clientB, _) = await RegisterOwnerAsync();
        var storeB = await CreateStoreAsync(clientB);

        // Reads of A's resources return 404 — not 403 — so nothing leaks.
        (await clientB.GetAsync($"/api/products/{productA.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Updates and deletes are blocked the same way.
        (await clientB.PatchAsJsonAsync($"/api/products/{productA.Id}/availability",
            new WhatsOrder.Application.Products.SetAvailabilityRequest(false), Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await clientB.DeleteAsync($"/api/products/{productA.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await clientB.PutAsJsonAsync($"/api/categories/{categoryA.Id}",
            new WhatsOrder.Application.Categories.SaveCategoryRequest("Hijacked", null, 0, true), Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // B's product list contains only B's products.
        var listB = await clientB.GetFromJsonAsync<WhatsOrder.Application.Common.PagedResult<
            WhatsOrder.Application.Products.ProductDto>>("/api/products", Json);
        listB!.Items.Should().NotContain(p => p.Name == "Secret Product A");

        // An order in A's store is invisible to B.
        var publicClient = Factory.CreateClient();
        var storeASlug = (await clientA.GetFromJsonAsync<StoreDto>("/api/store", Json))!.Slug;
        var orderResponse = await publicClient.PostAsJsonAsync($"/api/public/stores/{storeASlug}/orders",
            new CreatePublicOrderRequest("Customer", "92345678", FulfillmentMethod.Pickup,
                null, null, null, null, [new PublicOrderItemRequest(productA.Id, 1, null)]), Json);
        orderResponse.EnsureSuccessStatusCode();

        var ordersA = await clientA.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var orderId = ordersA!.Items.Single().Id;

        (await clientB.GetAsync($"/api/orders/{orderId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await clientB.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatus.Confirmed), Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // storeB exists purely so B is a fully set-up tenant.
        storeB.Should().NotBeNull();
    }
}
