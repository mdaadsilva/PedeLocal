namespace PedeLocal.Domain.Orders;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }

    // Snapshot fields — never recomputed from the live product.
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    public ICollection<OrderItemOption> Options { get; set; } = new List<OrderItemOption>();
}

public class OrderItemOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderItemId { get; set; }

    // Snapshot fields.
    public string OptionName { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
}
