using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using WhatsOrder.Application.Chat;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Application.Stores;
using WhatsOrder.Domain.Enums;
using Xunit;

namespace WhatsOrder.IntegrationTests;

[Collection("api")]
public class ChatAndRealtimeTests(TestAppFactory factory) : ApiTestBase(factory)
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<(HttpClient Owner, StoreDto Store, HttpClient Customer, PublicOrderCreatedDto Order)>
        SetUpStoreWithOrderAsync(string customerPhone = "92345678")
    {
        var (owner, _) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, "Karak Tea", 0.500m, stock: 50);

        var customer = Factory.CreateClient();
        var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest(
                "Fatma", customerPhone, FulfillmentMethod.Pickup,
                null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 2, null)]), Json);
        response.EnsureSuccessStatusCode();
        var order = (await response.Content.ReadFromJsonAsync<PublicOrderCreatedDto>(Json))!;
        return (owner, store, customer, order);
    }

    private HubConnection BuildHub(string queryOrToken, bool isOwner)
    {
        var url = isOwner
            ? $"{Factory.Server.BaseAddress}hubs/orders"
            : $"{Factory.Server.BaseAddress}hubs/orders?{queryOrToken}";

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (isOwner)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(queryOrToken);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
    }

    // ── Chat REST flow ─────────────────────────────────────────────────────

    [Fact]
    public async Task Chat_round_trip_with_read_state_and_unread_badges()
    {
        var (owner, store, customer, order) = await SetUpStoreWithOrderAsync();
        var chatUrl = $"/api/public/stores/{store.Slug}/orders/{order.OrderNumber}/messages?phone=92345678";

        // Customer asks a question.
        var sent = await customer.PostAsJsonAsync(chatUrl, new SendMessageRequest("Can you add extra sugar?"), Json);
        sent.EnsureSuccessStatusCode();

        // Owner's order list shows one unread message.
        var orders = await owner.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var listItem = orders!.Items.Single(o => o.OrderNumber == order.OrderNumber);
        listItem.UnreadMessages.Should().Be(1);

        // Owner opens the conversation → message is there and becomes read.
        var conversation = await owner.GetFromJsonAsync<List<ChatMessageDto>>(
            $"/api/orders/{listItem.Id}/messages", Json);
        conversation!.Should().ContainSingle(m => m.Sender == ChatSender.Customer
                                                  && m.Body == "Can you add extra sugar?");

        orders = await owner.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        orders!.Items.Single(o => o.OrderNumber == order.OrderNumber).UnreadMessages.Should().Be(0);

        // Owner replies; the customer sees both sides, and the reply is marked read
        // by the customer's GET (read receipt for the store).
        var reply = await owner.PostAsJsonAsync($"/api/orders/{listItem.Id}/messages",
            new SendMessageRequest("Of course! 🙌"), Json);
        reply.EnsureSuccessStatusCode();

        var customerView = await customer.GetFromJsonAsync<List<ChatMessageDto>>(chatUrl, Json);
        customerView!.Should().HaveCount(2);
        customerView.Single(m => m.Sender == ChatSender.Store).Body.Should().Be("Of course! 🙌");

        var ownerView = await owner.GetFromJsonAsync<List<ChatMessageDto>>(
            $"/api/orders/{listItem.Id}/messages", Json);
        ownerView!.Single(m => m.Sender == ChatSender.Store).ReadAt.Should().NotBeNull();

        // Customer's own message shows as read too (owner loaded it above).
        customerView.Single(m => m.Sender == ChatSender.Customer).ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Empty_or_oversized_messages_are_rejected()
    {
        var (_, store, customer, order) = await SetUpStoreWithOrderAsync();
        var chatUrl = $"/api/public/stores/{store.Slug}/orders/{order.OrderNumber}/messages?phone=92345678";

        (await customer.PostAsJsonAsync(chatUrl, new SendMessageRequest("   "), Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await customer.PostAsJsonAsync(chatUrl, new SendMessageRequest(new string('x', 1001)), Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Authorization ──────────────────────────────────────────────────────

    [Fact]
    public async Task Chat_is_inaccessible_without_the_right_phone_or_store()
    {
        var (ownerA, store, customer, order) = await SetUpStoreWithOrderAsync();

        // Wrong phone → the order effectively does not exist.
        var wrongPhone = $"/api/public/stores/{store.Slug}/orders/{order.OrderNumber}/messages?phone=99999999";
        (await customer.GetAsync(wrongPhone)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await customer.PostAsJsonAsync(wrongPhone, new SendMessageRequest("hi"), Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A different owner (their own store) cannot touch store A's order.
        var (ownerB, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(ownerB);
        var orders = await ownerA.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var orderId = orders!.Items.Single(o => o.OrderNumber == order.OrderNumber).Id;

        (await ownerB.GetAsync($"/api/orders/{orderId}/messages"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ownerB.PostAsJsonAsync($"/api/orders/{orderId}/messages", new SendMessageRequest("hi"), Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Anonymous callers cannot use the owner chat endpoint at all.
        var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync($"/api/orders/{orderId}/messages"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Status history, estimated time, rejection ──────────────────────────

    [Fact]
    public async Task Status_changes_are_recorded_with_history_estimate_and_system_message()
    {
        var (owner, store, customer, order) = await SetUpStoreWithOrderAsync();
        var orders = await owner.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var orderId = orders!.Items.Single(o => o.OrderNumber == order.OrderNumber).Id;

        // Accept with a 30-minute promise.
        var accept = await owner.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatus.Confirmed, EstimatedMinutes: 30), Json);
        accept.EnsureSuccessStatusCode();
        var detail = (await accept.Content.ReadFromJsonAsync<OrderDto>(Json))!;

        detail.EstimatedReadyAt.Should().NotBeNull();
        detail.EstimatedReadyAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(2));

        // Timeline: placed (by customer) then confirmed (by store).
        detail.StatusHistory.Should().HaveCount(2);
        detail.StatusHistory[0].Should().Match<OrderStatusHistoryDto>(h =>
            h.PreviousStatus == null && h.NewStatus == OrderStatus.New && h.ChangedBy == "customer");
        detail.StatusHistory[1].Should().Match<OrderStatusHistoryDto>(h =>
            h.PreviousStatus == OrderStatus.New && h.NewStatus == OrderStatus.Confirmed && h.ChangedBy == "store");

        // The customer's tracking endpoint exposes the same timeline.
        var publicStatus = await customer.GetFromJsonAsync<PublicOrderStatusDto>(
            $"/api/public/stores/{store.Slug}/orders/{order.OrderNumber}?phone=92345678", Json);
        publicStatus!.Status.Should().Be(OrderStatus.Confirmed);
        publicStatus.StatusHistory.Should().HaveCount(2);
        publicStatus.EstimatedReadyAt.Should().NotBeNull();

        // A system message landed in the conversation as a machine token.
        var conversation = await customer.GetFromJsonAsync<List<ChatMessageDto>>(
            $"/api/public/stores/{store.Slug}/orders/{order.OrderNumber}/messages?phone=92345678", Json);
        conversation!.Should().ContainSingle(m => m.Sender == ChatSender.System && m.Body == "status:Confirmed");
    }

    [Fact]
    public async Task Rejecting_is_only_possible_while_new_and_restocks_items()
    {
        var (owner, store, customer, order) = await SetUpStoreWithOrderAsync();
        var orders = await owner.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var orderId = orders!.Items.Single(o => o.OrderNumber == order.OrderNumber).Id;

        // Reject the fresh order → stock returns (50 - 2 + 2 = 50).
        var reject = await owner.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatus.Rejected), Json);
        reject.EnsureSuccessStatusCode();
        (await reject.Content.ReadFromJsonAsync<OrderDto>(Json))!.Status.Should().Be(OrderStatus.Rejected);

        await Factory.WithDbAsync(async db =>
        {
            var product = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .SingleAsync(db.Products, p => p.StoreId == store.Id && p.Name == "Karak Tea");
            product.StockQuantity.Should().Be(50);
        });

        // Terminal: nothing moves after rejection.
        (await owner.PatchAsJsonAsync($"/api/orders/{orderId}/status",
                new UpdateOrderStatusRequest(OrderStatus.Confirmed), Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // And a non-New order can no longer be rejected (cancel instead).
        var (owner2, store2, _, order2) = await SetUpStoreWithOrderAsync();
        var orders2 = await owner2.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var order2Id = orders2!.Items.Single(o => o.OrderNumber == order2.OrderNumber).Id;
        (await owner2.PatchAsJsonAsync($"/api/orders/{order2Id}/status",
                new UpdateOrderStatusRequest(OrderStatus.Confirmed), Json)).EnsureSuccessStatusCode();
        (await owner2.PatchAsJsonAsync($"/api/orders/{order2Id}/status",
                new UpdateOrderStatusRequest(OrderStatus.Rejected), Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _ = store2;
    }

    // ── Owner notifications ────────────────────────────────────────────────

    [Fact]
    public async Task Owner_notifications_are_persisted_and_scoped_per_owner()
    {
        var (owner, store, customer, order) = await SetUpStoreWithOrderAsync();

        // New order produced a persisted notification.
        var inbox = await owner.GetFromJsonAsync<NotificationsPage>("/api/notifications", Json);
        inbox!.UnreadCount.Should().BeGreaterThanOrEqualTo(1);
        inbox.Items.Should().Contain(n => n.Type == NotificationType.NewOrder
                                          && n.Title.Contains(order.OrderNumber));

        // A customer message adds a NewMessage notification.
        await customer.PostAsJsonAsync(
            $"/api/public/stores/{store.Slug}/orders/{order.OrderNumber}/messages?phone=92345678",
            new SendMessageRequest("Hello!"), Json);
        inbox = await owner.GetFromJsonAsync<NotificationsPage>("/api/notifications", Json);
        inbox!.Items.Should().Contain(n => n.Type == NotificationType.NewMessage);

        // Mark all read.
        (await owner.PostAsync("/api/notifications/read-all", null)).EnsureSuccessStatusCode();
        inbox = await owner.GetFromJsonAsync<NotificationsPage>("/api/notifications", Json);
        inbox!.UnreadCount.Should().Be(0);

        // Another owner sees none of it.
        var (ownerB, _) = await RegisterOwnerAsync();
        await CreateStoreAsync(ownerB);
        var inboxB = await ownerB.GetFromJsonAsync<NotificationsPage>("/api/notifications", Json);
        inboxB!.Items.Should().BeEmpty();
    }

    // ── Orders work without WhatsApp entirely ──────────────────────────────

    [Fact]
    public async Task Store_without_whatsapp_receives_orders_and_notifications()
    {
        var (owner, _) = await RegisterOwnerAsync();
        var create = await owner.PostAsJsonAsync("/api/store", new CreateStoreRequest(
            "No WhatsApp Store", null, UniqueSlug(), WhatsAppNumber: null,
            null, null, null, null, null, null), Json);
        create.EnsureSuccessStatusCode();
        var store = (await create.Content.ReadFromJsonAsync<StoreDto>(Json))!;
        store.WhatsAppNumber.Should().BeEmpty();

        var product = await CreateProductAsync(owner, "Halwa", 2.000m);
        var customer = Factory.CreateClient();
        var response = await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest(
                "Said", "97654321", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 1, null)]), Json);
        response.EnsureSuccessStatusCode();

        // The in-app channel delivered regardless of WhatsApp.
        var inbox = await owner.GetFromJsonAsync<NotificationsPage>("/api/notifications", Json);
        inbox!.Items.Should().Contain(n => n.Type == NotificationType.NewOrder);
    }

    // ── SignalR real-time delivery ─────────────────────────────────────────

    [Fact]
    public async Task Customer_receives_status_change_and_chat_in_real_time()
    {
        var (owner, store, _, order) = await SetUpStoreWithOrderAsync();
        var orders = await owner.GetFromJsonAsync<OrdersPage>("/api/orders", Json);
        var orderId = orders!.Items.Single(o => o.OrderNumber == order.OrderNumber).Id;

        await using var hub = BuildHub(
            $"slug={store.Slug}&orderNumber={order.OrderNumber}&phone=92345678", isOwner: false);

        var statusReceived = new TaskCompletionSource<OrderStatusChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var messageReceived = new TaskCompletionSource<ChatMessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<OrderStatusChangedEvent>("orderStatusChanged", e => statusReceived.TrySetResult(e));
        hub.On<ChatMessageDto>("messageReceived", m =>
        {
            if (m.Sender == ChatSender.Store)
                messageReceived.TrySetResult(m);
        });
        await hub.StartAsync();
        // Ping waits for OnConnectedAsync → group membership is guaranteed before we trigger events.
        (await hub.InvokeAsync<string>("Ping")).Should().Be("pong");

        // Owner confirms → customer's live event.
        (await owner.PatchAsJsonAsync($"/api/orders/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatus.Confirmed), Json)).EnsureSuccessStatusCode();
        var statusEvent = await statusReceived.Task.WaitAsync(EventTimeout);
        statusEvent.OrderNumber.Should().Be(order.OrderNumber);
        statusEvent.Status.Should().Be(OrderStatus.Confirmed);

        // Owner replies in chat → customer's live message.
        (await owner.PostAsJsonAsync($"/api/orders/{orderId}/messages",
            new SendMessageRequest("On it!"), Json)).EnsureSuccessStatusCode();
        (await messageReceived.Task.WaitAsync(EventTimeout)).Body.Should().Be("On it!");
    }

    [Fact]
    public async Task Owner_dashboard_receives_new_orders_in_real_time()
    {
        var (owner, auth) = await RegisterOwnerAsync();
        var store = await CreateStoreAsync(owner);
        var product = await CreateProductAsync(owner, "Luqaimat", 1.500m);

        await using var hub = BuildHub(auth.AccessToken, isOwner: true);
        var orderReceived = new TaskCompletionSource<OrderListItemDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationReceived = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<OrderListItemDto>("orderCreated", o => orderReceived.TrySetResult(o));
        hub.On<NotificationDto>("notification", n => notificationReceived.TrySetResult(n));
        await hub.StartAsync();
        // Ping waits for OnConnectedAsync → group membership is guaranteed before we trigger events.
        (await hub.InvokeAsync<string>("Ping")).Should().Be("pong");

        var customer = Factory.CreateClient();
        (await customer.PostAsJsonAsync($"/api/public/stores/{store.Slug}/orders",
            new CreatePublicOrderRequest(
                "Maryam", "96665544", FulfillmentMethod.Pickup, null, null, null, null,
                [new PublicOrderItemRequest(product.Id, 3, null)]), Json)).EnsureSuccessStatusCode();

        var live = await orderReceived.Task.WaitAsync(EventTimeout);
        live.CustomerName.Should().Be("Maryam");
        live.Total.Should().Be(4.500m);
        (await notificationReceived.Task.WaitAsync(EventTimeout)).Type.Should().Be(NotificationType.NewOrder);
    }

    [Fact]
    public async Task Hub_rejects_customers_with_a_wrong_phone()
    {
        var (_, store, _, order) = await SetUpStoreWithOrderAsync();

        await using var hub = BuildHub(
            $"slug={store.Slug}&orderNumber={order.OrderNumber}&phone=99998888", isOwner: false);

        var closed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Closed += _ => { closed.TrySetResult(null); return Task.CompletedTask; };

        try
        {
            await hub.StartAsync();
        }
        catch
        {
            return; // Rejected during start — also acceptable.
        }

        // The server aborts the connection during OnConnectedAsync; no group was ever
        // joined, so nothing can leak — the connection just dies immediately.
        await closed.Task.WaitAsync(EventTimeout);
        hub.State.Should().Be(HubConnectionState.Disconnected);
    }
}
