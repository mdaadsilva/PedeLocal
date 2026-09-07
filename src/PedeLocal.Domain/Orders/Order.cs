using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Orders;

public class Order : BaseEntity
{
    public Guid CustomerUserId { get; set; }
    public Guid EstablishmentId { get; set; }
    public Guid CityId { get; set; }
    /// <summary>Null for PICKUP orders — there is no delivery address to snapshot against.</summary>
    public Guid? AddressId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public OrderType Type { get; set; } = OrderType.Delivery;

    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public decimal? ChangeForAmount { get; set; }
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }

    public decimal? CalculatedDistanceKm { get; set; }
    public string PricingRuleSnapshotJson { get; set; } = "{}";

    /// <summary>Client-supplied key to make order creation idempotent against duplicate submits.</summary>
    public string? IdempotencyKey { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}
