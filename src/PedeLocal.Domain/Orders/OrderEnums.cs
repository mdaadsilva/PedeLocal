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
    Cancelled
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
