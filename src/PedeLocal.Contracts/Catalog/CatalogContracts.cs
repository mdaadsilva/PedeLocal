namespace PedeLocal.Contracts.Catalog;

public record MenuCategoryResponse(Guid Id, Guid EstablishmentId, string Name, int SortOrder);

public record CreateMenuCategoryRequest(string Name, int SortOrder);

public record UpdateMenuCategoryRequest(string Name, int SortOrder);

public record ProductOptionRequest(string Name, decimal PriceDelta, bool IsAvailable);

public record ProductOptionResponse(Guid Id, string Name, decimal PriceDelta, bool IsAvailable);

public record ProductOptionGroupRequest(string Name, bool Required, int Min, int Max, List<ProductOptionRequest> Options);

public record ProductOptionGroupResponse(Guid Id, string Name, bool Required, int Min, int Max, List<ProductOptionResponse> Options);

public record CreateProductRequest(
    Guid MenuCategoryId,
    string Name,
    string? Description,
    string? ImageUrl,
    decimal Price,
    decimal? PromotionalPrice,
    string StockMode,
    int? StockQuantity,
    List<ProductOptionGroupRequest> OptionGroups);

public record UpdateProductRequest(
    Guid MenuCategoryId,
    string Name,
    string? Description,
    string? ImageUrl,
    decimal Price,
    decimal? PromotionalPrice,
    List<ProductOptionGroupRequest> OptionGroups);

public record UpdateProductAvailabilityRequest(bool IsAvailable);

public record ProductResponse(
    Guid Id,
    Guid EstablishmentId,
    Guid MenuCategoryId,
    string Name,
    string? Description,
    string? ImageUrl,
    decimal Price,
    decimal? PromotionalPrice,
    string StockMode,
    int? StockQuantity,
    bool IsAvailable,
    List<ProductOptionGroupResponse> OptionGroups);
