using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WhatsOrder.Application.Account;
using WhatsOrder.Application.Auth;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Marketplace;
using WhatsOrder.Application.Offers;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Products;
using WhatsOrder.Application.Public;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Application.Reviews;
using WhatsOrder.Application.Stores;
using WhatsOrder.Domain.Enums;
using Xunit;

namespace WhatsOrder.IntegrationTests;

[Collection("api")]
public class BuyerIdentityTests(TestAppFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Buyer_registration_gets_buyer_role_only_and_no_owner_access()
    {
        var (buyer, auth) = await RegisterBuyerAsync();

        auth.User.Roles.Should().BeEquivalentTo(["Buyer"]);
        auth.User.HasStore.Should().BeFalse();

        // Owner-only endpoints are off limits.
        var products = await buyer.GetAsync("/api/products");
        products.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Account endpoints work.
        var overview = await buyer.GetAsync("/api/account");
        overview.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Owner_registration_also_gets_buyer_role_and_account_access()
    {
        var (owner, auth) = await RegisterOwnerAsync();

        auth.User.Roles.Should().Contain("Owner").And.Contain("Buyer");

        var overview = await owner.GetAsync("/api/account");
        overview.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Buyer_can_open_a_store_and_becomes_owner_after_token_refresh()
    {
        var (buyer, auth) = await RegisterBuyerAsync();
        auth.User.Roles.Should().BeEquivalentTo(["Buyer"]);

        // A buyer may create a store with the same account.
        var store = await CreateStoreAsync(buyer);
        store.Slug.Should().NotBeNullOrEmpty();

        // The pre-upgrade access token still lacks the Owner role...
        (await buyer.GetAsync("/api/products")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // ...refreshing issues a token that carries it.
        var refresh = await buyer.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken), Json);
        refresh.EnsureSuccessStatusCode();
        var upgraded = (await refresh.Content.ReadFromJsonAsync<AuthResponse>(Json))!;

        upgraded.User.Roles.Should().Contain("Owner").And.Contain("Buyer");
        upgraded.User.HasStore.Should().BeTrue();

        buyer.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", upgraded.AccessToken);
        (await buyer.GetAsync("/api/products")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await buyer.GetAsync("/api/account")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Buyer_profile_can_be_updated_and_read_back()
    {
        var (buyer, _) = await RegisterBuyerAsync();

        var update = await buyer.PutAsJsonAsync("/api/account/profile",
            new UpdateBuyerProfileRequest("New Name", "92345678", "ar", false, true, false), Json);
        update.EnsureSuccessStatusCode();

        var profile = (await buyer.GetFromJsonAsync<BuyerProfileDto>("/api/account/profile", Json))!;
        profile.DisplayName.Should().Be("New Name");
        profile.Phone.Should().Be("+96892345678");
        profile.PreferredLanguage.Should().Be("ar");
        profile.NotifyOrderUpdates.Should().BeFalse();
        profile.NotifyOffers.Should().BeFalse();
    }
}

[Collection("api")]
public class FavoritesAndAddressTests(TestAppFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Favorites_add_list_and_remove_for_stores_and_products()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, "Fav Cookies");

        var (buyer, _) = await RegisterBuyerAsync();
        var publicStore = (await buyer.GetFromJsonAsync<PublicStoreDto>($"/api/public/stores/{store.Slug}", Json))!;

        (await buyer.PostAsync($"/api/favorites/stores/{publicStore.Id}", null)).EnsureSuccessStatusCode();
        (await buyer.PostAsync($"/api/favorites/products/{product.Id}", null)).EnsureSuccessStatusCode();
        // Adding twice is idempotent.
        (await buyer.PostAsync($"/api/favorites/stores/{publicStore.Id}", null)).EnsureSuccessStatusCode();

        var ids = (await buyer.GetFromJsonAsync<FavoriteIdsDto>("/api/account/favorites/ids", Json))!;
        ids.StoreIds.Should().ContainSingle().Which.Should().Be(publicStore.Id);
        ids.ProductIds.Should().ContainSingle().Which.Should().Be(product.Id);

        var favorites = (await buyer.GetFromJsonAsync<FavoritesDto>("/api/account/favorites", Json))!;
        favorites.Stores.Should().ContainSingle(s => s.Slug == store.Slug);
        favorites.Products.Should().ContainSingle(p => p.Name == "Fav Cookies" && p.StoreSlug == store.Slug);

        (await buyer.DeleteAsync($"/api/favorites/stores/{publicStore.Id}")).EnsureSuccessStatusCode();
        (await buyer.DeleteAsync($"/api/favorites/products/{product.Id}")).EnsureSuccessStatusCode();

        var after = (await buyer.GetFromJsonAsync<FavoriteIdsDto>("/api/account/favorites/ids", Json))!;
        after.StoreIds.Should().BeEmpty();
        after.ProductIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Favorites_are_private_per_user()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var (buyer1, _) = await RegisterBuyerAsync();
        var (buyer2, _) = await RegisterBuyerAsync();

        var publicStore = (await buyer1.GetFromJsonAsync<PublicStoreDto>($"/api/public/stores/{store.Slug}", Json))!;
        (await buyer1.PostAsync($"/api/favorites/stores/{publicStore.Id}", null)).EnsureSuccessStatusCode();

        var other = (await buyer2.GetFromJsonAsync<FavoriteIdsDto>("/api/account/favorites/ids", Json))!;
        other.StoreIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Addresses_crud_with_default_handling()
    {
        var (buyer, _) = await RegisterBuyerAsync();

        SaveAddressRequest Address(string label, bool isDefault = false) => new(
            label, "Ahmed", "91234567", "Muscat", "Bawshar", null, "Al Khuwair",
            "Street 1", "Bldg 5", "Apt 2", "Ring the bell", 23.5880, 58.3829, isDefault);

        // First address becomes the default automatically.
        var home = (await (await buyer.PostAsJsonAsync("/api/account/addresses", Address("Home"), Json))
            .Content.ReadFromJsonAsync<BuyerAddressDto>(Json))!;
        home.IsDefault.Should().BeTrue();
        home.Phone.Should().Be("+96891234567");

        // A second default address takes over.
        var work = (await (await buyer.PostAsJsonAsync("/api/account/addresses", Address("Work", true), Json))
            .Content.ReadFromJsonAsync<BuyerAddressDto>(Json))!;
        work.IsDefault.Should().BeTrue();

        var list = (await buyer.GetFromJsonAsync<List<BuyerAddressDto>>("/api/account/addresses", Json))!;
        list.Should().HaveCount(2);
        list.Single(a => a.IsDefault).Label.Should().Be("Work");

        // Deleting the default promotes the remaining address.
        (await buyer.DeleteAsync($"/api/account/addresses/{work.Id}")).EnsureSuccessStatusCode();
        list = (await buyer.GetFromJsonAsync<List<BuyerAddressDto>>("/api/account/addresses", Json))!;
        list.Should().ContainSingle().Which.IsDefault.Should().BeTrue();

        // Invalid phone is rejected with the stable code.
        var bad = await buyer.PostAsJsonAsync("/api/account/addresses",
            Address("Bad") with { Phone = "abc" }, Json);
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Addresses_are_private_per_user()
    {
        var (buyer1, _) = await RegisterBuyerAsync();
        var (buyer2, _) = await RegisterBuyerAsync();

        var created = (await (await buyer1.PostAsJsonAsync("/api/account/addresses",
                new SaveAddressRequest("Home", "Ahmed", "91234567", null, null, null, null,
                    null, null, null, null, null, null), Json))
            .Content.ReadFromJsonAsync<BuyerAddressDto>(Json))!;

        // Another user can neither read nor delete it.
        (await buyer2.GetFromJsonAsync<List<BuyerAddressDto>>("/api/account/addresses", Json))!
            .Should().BeEmpty();
        var delete = await buyer2.DeleteAsync($"/api/account/addresses/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

[Collection("api")]
public class BuyerOrderAndChatTests(TestAppFactory factory) : ApiTestBase(factory)
{
    private static CreatePublicOrderRequest OrderRequest(Guid productId, int quantity = 2) => new(
        "Ahmed Buyer", "98765432", FulfillmentMethod.Pickup, null, null, null, null,
        [new PublicOrderItemRequest(productId, quantity, null)]);

    [Fact]
    public async Task Signed_in_buyer_order_lands_in_account_history_with_store_info()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, "Karak Tea", price: 1.500m);

        var (buyer, _) = await RegisterBuyerAsync();
        var created = await buyer.PostAsJsonAsync(
            $"/api/public/stores/{store.Slug}/orders", OrderRequest(product.Id), Json);
        created.EnsureSuccessStatusCode();

        var page = (await buyer.GetFromJsonAsync<BuyerOrdersPage>("/api/account/orders", Json))!;
        page.Total.Should().Be(1);
        page.ActiveCount.Should().Be(1);
        var item = page.Items.Single();
        item.StoreSlug.Should().Be(store.Slug);
        item.Status.Should().Be(OrderStatus.New);
        item.ItemsSummary.Should().Contain("Karak Tea");

        var detail = (await buyer.GetFromJsonAsync<BuyerOrderDto>($"/api/account/orders/{item.Id}", Json))!;
        detail.StoreName.Should().Be(store.Name);
        detail.Items.Should().ContainSingle();
        detail.CanReview.Should().BeFalse();
    }

    [Fact]
    public async Task Anonymous_orders_stay_unlinked_and_other_buyers_cannot_see_my_order()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        // Anonymous checkout — no account should ever see this order.
        var anonymous = Factory.CreateClient();
        (await anonymous.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            OrderRequest(product.Id), Json)).EnsureSuccessStatusCode();

        var (buyer, _) = await RegisterBuyerAsync();
        var created = await buyer.PostAsJsonAsync(
            $"/api/public/stores/{store.Slug}/orders", OrderRequest(product.Id), Json);
        created.EnsureSuccessStatusCode();

        var mine = (await buyer.GetFromJsonAsync<BuyerOrdersPage>("/api/account/orders", Json))!;
        mine.Total.Should().Be(1);

        var (otherBuyer, _) = await RegisterBuyerAsync();
        var otherView = await otherBuyer.GetAsync($"/api/account/orders/{mine.Items.Single().Id}");
        otherView.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Buyer_chat_reaches_owner_and_conversations_inbox_tracks_unread()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        var (buyer, _) = await RegisterBuyerAsync();
        (await buyer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            OrderRequest(product.Id), Json)).EnsureSuccessStatusCode();
        var orderId = (await buyer.GetFromJsonAsync<BuyerOrdersPage>("/api/account/orders", Json))!
            .Items.Single().Id;

        // Buyer sends through the account endpoint.
        var sent = await buyer.PostAsJsonAsync($"/api/account/orders/{orderId}/messages",
            new { body = "Please add extra napkins" }, Json);
        sent.EnsureSuccessStatusCode();

        // Owner sees it in the same conversation and replies.
        var ownerThread = (await owner.GetFromJsonAsync<List<ChatMessageDto>>(
            $"/api/orders/{orderId}/messages", Json))!;
        ownerThread.Should().Contain(m => m.Body == "Please add extra napkins" && m.Sender == ChatSender.Customer);
        (await owner.PostAsJsonAsync($"/api/orders/{orderId}/messages",
            new { body = "Sure thing!" }, Json)).EnsureSuccessStatusCode();

        // The reply shows as unread in the buyer's inbox…
        var conversations = (await buyer.GetFromJsonAsync<List<BuyerConversationDto>>(
            "/api/account/conversations", Json))!;
        var convo = conversations.Should().ContainSingle(c => c.OrderId == orderId).Subject;
        convo.LastMessage.Should().Be("Sure thing!");
        convo.UnreadCount.Should().Be(1);
        convo.StoreSlug.Should().Be(store.Slug);

        // …and buyer notifications recorded the store reply.
        var notifications = (await buyer.GetFromJsonAsync<NotificationsPage>("/api/notifications", Json))!;
        notifications.UnreadCount.Should().BeGreaterThan(0);

        // Reading the thread clears the unread badge.
        (await buyer.GetFromJsonAsync<List<ChatMessageDto>>(
            $"/api/account/orders/{orderId}/messages", Json)).Should().NotBeNull();
        conversations = (await buyer.GetFromJsonAsync<List<BuyerConversationDto>>(
            "/api/account/conversations", Json))!;
        conversations.Single(c => c.OrderId == orderId).UnreadCount.Should().Be(0);

        // A stranger cannot touch the conversation.
        var (stranger, _) = await RegisterBuyerAsync();
        var strangerRead = await stranger.GetAsync($"/api/account/orders/{orderId}/messages");
        strangerRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

[Collection("api")]
public class ReviewTests(TestAppFactory factory) : ApiTestBase(factory)
{
    private async Task<(HttpClient Owner, HttpClient Buyer, StoreDto Store, Guid OrderId)> PlaceOrderAsync()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        var (buyer, _) = await RegisterBuyerAsync("Reviewer Ahmed");
        (await buyer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ahmed", "98765432", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 1, null)]), Json)).EnsureSuccessStatusCode();
        var orderId = (await buyer.GetFromJsonAsync<BuyerOrdersPage>("/api/account/orders", Json))!
            .Items.Single().Id;
        return (owner, buyer, store, orderId);
    }

    private async Task CompleteOrderAsync(HttpClient owner, Guid orderId)
    {
        foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed })
        {
            var response = await owner.PatchAsJsonAsync($"/api/orders/{orderId}/status",
                new UpdateOrderStatusRequest(status), Json);
            response.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Only_completed_orders_can_be_reviewed_and_only_once()
    {
        var (owner, buyer, store, orderId) = await PlaceOrderAsync();

        // Not completed yet → rejected.
        var early = await buyer.PostAsJsonAsync($"/api/account/orders/{orderId}/review",
            new CreateReviewRequest(5, "Great!"), Json);
        early.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await CompleteOrderAsync(owner, orderId);

        var review = await buyer.PostAsJsonAsync($"/api/account/orders/{orderId}/review",
            new CreateReviewRequest(4, "Lovely, arrived fresh."), Json);
        review.EnsureSuccessStatusCode();
        (await review.Content.ReadFromJsonAsync<ReviewDto>(Json))!.ReviewerName.Should().Be("Reviewer Ahmed");

        // Second attempt on the same order fails.
        var again = await buyer.PostAsJsonAsync($"/api/account/orders/{orderId}/review",
            new CreateReviewRequest(1, "changed my mind"), Json);
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Rating shows up publicly: on the store page and in the reviews list.
        var publicStore = (await Factory.CreateClient()
            .GetFromJsonAsync<PublicStoreDto>($"/api/public/stores/{store.Slug}", Json))!;
        publicStore.Rating.Should().Be(4);
        publicStore.ReviewsCount.Should().Be(1);

        var reviewsPage = (await Factory.CreateClient().GetFromJsonAsync<StoreReviewsPage>(
            $"/api/public/stores/{store.Slug}/reviews", Json))!;
        reviewsPage.Items.Should().ContainSingle(r => r.Comment == "Lovely, arrived fresh.");
        reviewsPage.AverageRating.Should().Be(4);

        // The owner sees it too.
        var ownerReviews = (await owner.GetFromJsonAsync<StoreReviewsPage>("/api/store/reviews", Json))!;
        ownerReviews.Total.Should().Be(1);
    }

    [Fact]
    public async Task A_stranger_cannot_review_someone_elses_order()
    {
        var (owner, _, _, orderId) = await PlaceOrderAsync();
        await CompleteOrderAsync(owner, orderId);

        var (stranger, _) = await RegisterBuyerAsync();
        var attempt = await stranger.PostAsJsonAsync($"/api/account/orders/{orderId}/review",
            new CreateReviewRequest(5, null), Json);
        attempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

[Collection("api")]
public class OfferTests(TestAppFactory factory) : ApiTestBase(factory)
{
    private static SaveOfferRequest Percentage10(decimal minimum = 0) => new(
        "10% off", "خصم ١٠٪", null, null, OfferType.Percentage, 10, minimum,
        DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));

    [Fact]
    public async Task Percentage_offer_is_applied_automatically_at_checkout()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, price: 3.000m);

        (await owner.PostAsJsonAsync("/api/offers", Percentage10(), Json)).EnsureSuccessStatusCode();

        // Offer surfaces on the public store page.
        var publicStore = (await Factory.CreateClient()
            .GetFromJsonAsync<PublicStoreDto>($"/api/public/stores/{store.Slug}", Json))!;
        publicStore.Offers.Should().ContainSingle(o => o.Title == "10% off");

        var order = await Factory.CreateClient().PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ahmed", "98765432", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 2, null)]), Json);
        order.EnsureSuccessStatusCode();
        var created = (await order.Content.ReadFromJsonAsync<PublicOrderCreatedDto>(Json))!;

        created.Subtotal.Should().Be(6.000m);
        created.Discount.Should().Be(0.600m);
        created.Total.Should().Be(5.400m);
        created.AppliedOffer.Should().Be("10% off");
    }

    [Fact]
    public async Task Offer_below_minimum_or_expired_is_not_applied()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, price: 3.000m);

        // Minimum higher than the order subtotal.
        (await owner.PostAsJsonAsync("/api/offers", Percentage10(minimum: 50), Json)).EnsureSuccessStatusCode();
        // An expired offer must be ignored and never surface publicly.
        (await owner.PostAsJsonAsync("/api/offers", new SaveOfferRequest(
            "Old promo", null, null, null, OfferType.Percentage, 50, 0,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-3)), Json)).EnsureSuccessStatusCode();

        var publicStore = (await Factory.CreateClient()
            .GetFromJsonAsync<PublicStoreDto>($"/api/public/stores/{store.Slug}", Json))!;
        publicStore.Offers.Should().NotContain(o => o.Title == "Old promo");

        var order = await Factory.CreateClient().PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ahmed", "98765432", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 1, null)]), Json);
        var created = (await order.Content.ReadFromJsonAsync<PublicOrderCreatedDto>(Json))!;
        created.Discount.Should().Be(0);
        created.Total.Should().Be(3.000m);
        created.AppliedOffer.Should().BeNull();
    }

    [Fact]
    public async Task Free_delivery_offer_waives_the_fee()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, price: 3.000m);

        (await owner.PutAsJsonAsync("/api/store/settings", new UpdateStoreSettingsRequest(
            1.500m, 0, true, true, OpeningHours.Defaults(), "en"), Json)).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync("/api/offers", new SaveOfferRequest(
            "Free delivery", null, null, null, OfferType.FreeDelivery, 0, 0,
            DateTime.UtcNow.AddDays(-1), null), Json)).EnsureSuccessStatusCode();

        var order = await Factory.CreateClient().PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest("Ahmed", "98765432", FulfillmentMethod.Delivery,
                "Al Khuwair, Street 5", null, null, null,
                [new PublicOrderItemRequest(product.Id, 1, null)]), Json);
        order.EnsureSuccessStatusCode();
        var created = (await order.Content.ReadFromJsonAsync<PublicOrderCreatedDto>(Json))!;

        created.DeliveryFee.Should().Be(0);
        created.Total.Should().Be(3.000m);
        created.AppliedOffer.Should().Be("Free delivery");
    }

    [Fact]
    public async Task Owners_manage_only_their_own_offers()
    {
        var (owner1, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(owner1);
        var offer = (await (await owner1.PostAsJsonAsync("/api/offers", Percentage10(), Json))
            .Content.ReadFromJsonAsync<OfferDto>(Json))!;

        var (owner2, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(owner2);
        (await owner2.GetFromJsonAsync<List<OfferDto>>("/api/offers", Json))!.Should().BeEmpty();
        var foreignDelete = await owner2.DeleteAsync($"/api/offers/{offer.Id}");
        foreignDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

[Collection("api")]
public class DiscoveryTests(TestAppFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Store_directory_lists_and_filters_by_search()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        await CreateProductAsync(owner);

        var anonymous = Factory.CreateClient();
        var page = (await anonymous.GetFromJsonAsync<StoresPage>(
            $"/api/public/stores?search={store.Slug}&sort=newest", Json))!;
        page.Items.Should().ContainSingle(s => s.Slug == store.Slug);

        var all = (await anonymous.GetFromJsonAsync<StoresPage>("/api/public/stores?pageSize=5", Json))!;
        all.Items.Count.Should().BeLessThanOrEqualTo(5);
        all.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Unified_search_finds_stores_and_products_and_records_recent_searches()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var unique = $"Zanzibari{Guid.NewGuid():N}"[..16];
        await CreateProductAsync(owner, $"{unique} Halwa", price: 2.000m);

        var (buyer, _) = await RegisterBuyerAsync();
        var results = (await buyer.GetFromJsonAsync<SearchResultsDto>(
            $"/api/public/search?q={unique}", Json))!;
        results.Products.Should().ContainSingle(p => p.Name.StartsWith(unique) && p.StoreSlug == store.Slug);
        results.ProductsTotal.Should().Be(1);

        var suggestions = (await buyer.GetFromJsonAsync<SearchSuggestionsDto>(
            "/api/public/search/suggest", Json))!;
        suggestions.Recent.Should().Contain(unique);

        var typed = (await buyer.GetFromJsonAsync<SearchSuggestionsDto>(
            $"/api/public/search/suggest?q={unique[..8]}", Json))!;
        typed.Suggestions.Should().Contain(s => s.StartsWith(unique));
    }

    [Fact]
    public async Task Home_returns_generic_sections_anonymously_and_personal_ones_when_signed_in()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, "Trending Kunafa", isFeatured: true);

        var anonymousHome = (await Factory.CreateClient()
            .GetFromJsonAsync<MarketplaceHomeDto>("/api/public/home", Json))!;
        anonymousHome.Sections.Should().Contain(s => s.Key == "popularStores");
        anonymousHome.Sections.Should().NotContain(s => s.Key == "recentlyViewed");

        // A buyer who viewed a product gets a recently-viewed rail.
        var (buyer, _) = await RegisterBuyerAsync();
        (await buyer.GetAsync($"/api/public/stores/{store.Slug}/products/{product.Id}"))
            .EnsureSuccessStatusCode();

        var personalHome = (await buyer.GetFromJsonAsync<MarketplaceHomeDto>("/api/public/home", Json))!;
        var recentlyViewed = personalHome.Sections.Should()
            .Contain(s => s.Key == "recentlyViewed").Subject;
        recentlyViewed.Products.Should().Contain(p => p.Id == product.Id);
        personalHome.Sections.Should().Contain(s => s.Key == "recommendedStores");
    }

    [Fact]
    public async Task Related_products_come_from_the_same_store()
    {
        var (owner, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(owner);
        var category = await CreateCategoryAsync(owner, "Sweets");
        var main = await CreateProductAsync(owner, "Main Halwa", categoryId: category.Id);
        await CreateProductAsync(owner, "Sibling Halwa", categoryId: category.Id);
        await CreateProductAsync(owner, "Other Item");

        var related = (await Factory.CreateClient().GetFromJsonAsync<List<ProductCardDto>>(
            $"/api/public/products/{main.Id}/related", Json))!;
        related.Should().NotContain(p => p.Id == main.Id);
        related.First().Name.Should().Be("Sibling Halwa"); // same category ranks first
    }
}

[Collection("api")]
public class BrandingAndImageTests(TestAppFactory factory) : ApiTestBase(factory)
{
    private static MultipartFormDataContent FakeImage(string filename = "photo.png")
    {
        var bytes = new byte[128];
        Random.Shared.NextBytes(bytes);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { content, "file", filename } };
    }

    [Fact]
    public async Task Store_banner_upload_and_removal()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);

        var upload = await owner.PostAsync("/api/store/banner", FakeImage("banner.png"));
        upload.EnsureSuccessStatusCode();
        var updated = (await upload.Content.ReadFromJsonAsync<StoreDto>(Json))!;
        updated.BannerUrl.Should().StartWith("/uploads/banners/");

        // The banner flows through to the public store page.
        var publicStore = (await Factory.CreateClient()
            .GetFromJsonAsync<PublicStoreDto>($"/api/public/stores/{store.Slug}", Json))!;
        publicStore.BannerUrl.Should().Be(updated.BannerUrl);

        var removed = (await (await owner.DeleteAsync("/api/store/banner"))
            .Content.ReadFromJsonAsync<StoreDto>(Json))!;
        removed.BannerUrl.Should().BeNull();
    }

    [Fact]
    public async Task Product_images_reorder_and_primary_selection()
    {
        var (owner, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner);

        var withOne = (await (await owner.PostAsync($"/api/products/{product.Id}/images", FakeImage("a.png")))
            .Content.ReadFromJsonAsync<ProductDto>(Json))!;
        var withTwo = (await (await owner.PostAsync($"/api/products/{product.Id}/images", FakeImage("b.png")))
            .Content.ReadFromJsonAsync<ProductDto>(Json))!;

        var first = withOne.Images.Single();
        var second = withTwo.Images.Single(i => i.Id != first.Id);
        first.IsPrimary.Should().BeTrue();

        // Reverse the order.
        var reordered = (await (await owner.PutAsJsonAsync($"/api/products/{product.Id}/images/order",
                new { imageIds = new[] { second.Id, first.Id } }, Json))
            .Content.ReadFromJsonAsync<ProductDto>(Json))!;
        reordered.Images.OrderBy(i => i.SortOrder).First().Id.Should().Be(second.Id);

        // Promote the second image to primary.
        var promoted = (await (await owner.PatchAsync(
                $"/api/products/{product.Id}/images/{second.Id}/primary", null))
            .Content.ReadFromJsonAsync<ProductDto>(Json))!;
        promoted.Images.Single(i => i.IsPrimary).Id.Should().Be(second.Id);

        // A different owner cannot touch these images.
        var (other, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(other);
        var foreign = await other.PatchAsync($"/api/products/{product.Id}/images/{second.Id}/primary", null);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
