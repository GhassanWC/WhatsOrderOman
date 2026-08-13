namespace WhatsOrder.Domain.Enums;

public enum OrderStatus
{
    New = 0,
    Confirmed = 1,
    Preparing = 2,
    Ready = 3,
    OutForDelivery = 4,
    Completed = 5,
    Cancelled = 6,
    /// <summary>Declined by the store while the order was still New. Terminal.</summary>
    Rejected = 7
}

/// <summary>Who authored a chat message on an order conversation.</summary>
public enum ChatSender
{
    Customer = 0,
    Store = 1,
    /// <summary>Automatic messages (status changes); Body holds a machine token like "status:Confirmed".</summary>
    System = 2
}

public enum NotificationType
{
    NewOrder = 0,
    OrderStatusChanged = 1,
    NewMessage = 2
}

public enum FulfillmentMethod
{
    Pickup = 0,
    Delivery = 1
}

/// <summary>Shopping signals recorded in BuyerActivity for recently-viewed and recommendations.</summary>
public enum BuyerEventType
{
    StoreViewed = 0,
    ProductViewed = 1,
    Searched = 2,
    CategoryViewed = 3,
    StoreFavorited = 4,
    ProductFavorited = 5,
    AddedToCart = 6,
    Ordered = 7
}

public enum OfferType
{
    /// <summary>DiscountValue is a percentage of the subtotal (0–100).</summary>
    Percentage = 0,
    /// <summary>DiscountValue is a fixed OMR amount off the subtotal.</summary>
    FixedAmount = 1,
    /// <summary>Waives the delivery fee; DiscountValue unused.</summary>
    FreeDelivery = 2
}

public enum SubscriptionPlan
{
    Free = 0,
    Pro = 1
}

public enum SubscriptionStatus
{
    Active = 0,
    PastDue = 1,
    Cancelled = 2
}

public enum WhatsAppDirection
{
    Outbound = 0,
    Inbound = 1
}

public enum WhatsAppMessageType
{
    OrderConfirmation = 0,
    OrderStatusUpdate = 1,
    OwnerNewOrderAlert = 2,
    Inbound = 3,
    Other = 4
}

public enum WhatsAppMessageStatus
{
    Pending = 0,
    Sent = 1,
    Delivered = 2,
    Read = 3,
    Failed = 4,
    /// <summary>WhatsApp disabled, not configured, or not included in the store's plan.</summary>
    Skipped = 5
}
