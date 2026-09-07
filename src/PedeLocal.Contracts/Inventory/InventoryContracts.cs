namespace PedeLocal.Contracts.Inventory;

public record AdjustStockRequest(string Type, int Quantity, string? Reason);

public record InventoryMovementResponse(
    Guid Id,
    Guid ProductId,
    string Type,
    int Quantity,
    int PreviousQuantity,
    int NewQuantity,
    Guid? OrderId,
    Guid? UserId,
    string? Reason,
    DateTime CreatedAtUtc);
