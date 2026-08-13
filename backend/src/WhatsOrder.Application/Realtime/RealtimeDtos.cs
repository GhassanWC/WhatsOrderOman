using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Realtime;

public sealed record ChatMessageDto(
    Guid Id,
    Guid OrderId,
    ChatSender Sender,
    string Body,
    DateTime SentAt,
    DateTime? ReadAt);

public sealed record OrderStatusHistoryDto(
    OrderStatus? PreviousStatus,
    OrderStatus NewStatus,
    string ChangedBy,
    DateTime ChangedAt);

/// <summary>Pushed to the order group and the store group when a status changes.</summary>
public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    string OrderNumber,
    OrderStatus Status,
    DateTime? EstimatedReadyAt,
    OrderStatusHistoryDto Entry);

/// <summary>Pushed to the order group when one side loads the other's messages.</summary>
public sealed record MessagesReadEvent(Guid OrderId, ChatSender Reader, DateTime ReadAt);

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    Guid? OrderId,
    bool IsRead,
    DateTime CreatedAt);

public sealed record NotificationsPage(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount);

public static class RealtimeMapping
{
    public static ChatMessageDto ToDto(this ChatMessage m) =>
        new(m.Id, m.OrderId, m.Sender, m.Body, m.CreatedAt, m.ReadAt);

    public static OrderStatusHistoryDto ToDto(this OrderStatusHistory h) =>
        new(h.PreviousStatus, h.NewStatus, h.ChangedBy, h.CreatedAt);

    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.Type, n.Title, n.Body, n.OrderId, n.IsRead, n.CreatedAt);
}
