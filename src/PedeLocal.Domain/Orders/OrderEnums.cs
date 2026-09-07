namespace PedeLocal.Domain.Orders;

public enum OrderStatus
{
    Pending,
    Accepted,
    Preparing,
    Ready,
    WaitingDriver,
    DriverAssigned,
    PickedUp,
    OutForDelivery,
    Delivered,
    /// <summary>Terminal state for PICKUP orders: the customer collected the order directly at the
    /// establishment. Distinct from <see cref="Delivered"/> (which always means a driver delivery)
    /// so the two fulfillment paths never collapse into the same status.</summary>
    PickupCompleted,
    Cancelled
}

public enum OrderType
{
    Delivery,
    Pickup
}

public enum PaymentMethod
{
    Pix,
    Cash,
    DebitOnDelivery,
    CreditOnDelivery
}

public enum PaymentStatus
{
    Pending,
    Paid,
    NotApplicable
}
