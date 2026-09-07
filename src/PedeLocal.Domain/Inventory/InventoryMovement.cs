namespace PedeLocal.Domain.Inventory;

public enum InventoryMovementType
{
    Entry,
    Sale,
    Adjustment,
    Loss,
    Return,
    CancellationRestore
}

public class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public InventoryMovementType Type { get; set; }
    public int Quantity { get; set; }
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? UserId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
