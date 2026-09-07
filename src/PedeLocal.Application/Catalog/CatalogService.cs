using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Catalog;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Inventory;

namespace PedeLocal.Application.Catalog;

public class CatalogService(IAppDbContext db, ICurrentUserService currentUser, EstablishmentAccessGuard accessGuard)
{
    // --- Menu categories ---

    public async Task<List<MenuCategoryResponse>> GetMenuCategoriesAsync(Guid establishmentId, CancellationToken ct) =>
        await db.MenuCategories
            .Where(c => c.EstablishmentId == establishmentId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new MenuCategoryResponse(c.Id, c.EstablishmentId, c.Name, c.SortOrder))
            .ToListAsync(ct);

    public async Task<MenuCategoryResponse> CreateMenuCategoryAsync(Guid establishmentId, CreateMenuCategoryRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);

        var category = new MenuCategory { EstablishmentId = establishmentId, Name = request.Name.Trim(), SortOrder = request.SortOrder };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return new MenuCategoryResponse(category.Id, category.EstablishmentId, category.Name, category.SortOrder);
    }

    public async Task<MenuCategoryResponse> UpdateMenuCategoryAsync(Guid establishmentId, Guid categoryId, UpdateMenuCategoryRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);

        var category = await db.MenuCategories.SingleOrDefaultAsync(c => c.Id == categoryId && c.EstablishmentId == establishmentId, ct)
            ?? throw new NotFoundException("MENU_CATEGORY_NOT_FOUND", "Categoria de cardápio não encontrada.");

        category.Name = request.Name.Trim();
        category.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(ct);
        return new MenuCategoryResponse(category.Id, category.EstablishmentId, category.Name, category.SortOrder);
    }

    // --- Products ---

    public async Task<List<ProductResponse>> GetProductsAsync(Guid establishmentId, CancellationToken ct) =>
        await db.Products
            .Where(p => p.EstablishmentId == establishmentId)
            .Include(p => p.OptionGroups).ThenInclude(g => g.Options)
            .OrderBy(p => p.Name)
            .Select(p => ToResponse(p))
            .ToListAsync(ct);

    public async Task<ProductResponse> GetProductByIdAsync(Guid establishmentId, Guid productId, CancellationToken ct)
    {
        var product = await LoadProductAsync(establishmentId, productId, ct);
        return ToResponse(product);
    }

    /// <summary>Customer-facing catalog: only available products from a visible, non-suspended establishment.</summary>
    public async Task<List<ProductResponse>> GetPublicProductsAsync(Guid establishmentId, CancellationToken ct)
    {
        var isPublic = await (
            from e in db.Establishments
            join s in db.EstablishmentSettings on e.Id equals s.EstablishmentId
            where e.Id == establishmentId && s.IsVisible
                  && (e.CommercialStatus == Domain.Establishments.CommercialStatus.Active
                      || e.CommercialStatus == Domain.Establishments.CommercialStatus.OverdueGrace)
            select e.Id).AnyAsync(ct);

        if (!isPublic)
        {
            throw new NotFoundException("ESTABLISHMENT_NOT_FOUND", "Estabelecimento não encontrado.");
        }

        return await db.Products
            .Where(p => p.EstablishmentId == establishmentId && p.IsAvailable)
            .Include(p => p.OptionGroups).ThenInclude(g => g.Options)
            .OrderBy(p => p.Name)
            .Select(p => ToResponse(p))
            .ToListAsync(ct);
    }

    public async Task<ProductResponse> CreateProductAsync(Guid establishmentId, CreateProductRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        await EnsureMenuCategoryBelongsAsync(establishmentId, request.MenuCategoryId, ct);

        if (!Enum.TryParse<StockMode>(request.StockMode, ignoreCase: true, out var stockMode))
        {
            throw new AppException("INVALID_STOCK_MODE", "Modo de estoque inválido.");
        }

        var product = new Product
        {
            EstablishmentId = establishmentId,
            MenuCategoryId = request.MenuCategoryId,
            Name = request.Name.Trim(),
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            Price = request.Price,
            PromotionalPrice = request.PromotionalPrice,
            StockMode = stockMode,
            StockQuantity = stockMode == StockMode.Controlled ? request.StockQuantity ?? 0 : null,
            IsAvailable = true
        };
        db.Products.Add(product);
        ApplyOptionGroups(db, product.Id, request.OptionGroups);

        if (stockMode == StockMode.Controlled && product.StockQuantity is > 0)
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                Type = InventoryMovementType.Entry,
                Quantity = product.StockQuantity.Value,
                PreviousQuantity = 0,
                NewQuantity = product.StockQuantity.Value,
                UserId = currentUser.UserId,
                Reason = "Estoque inicial na criação do produto."
            });
        }

        await db.SaveChangesAsync(ct);
        return await GetProductByIdAsync(establishmentId, product.Id, ct);
    }

    public async Task<ProductResponse> UpdateProductAsync(Guid establishmentId, Guid productId, UpdateProductRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        await EnsureMenuCategoryBelongsAsync(establishmentId, request.MenuCategoryId, ct);

        var product = await LoadProductAsync(establishmentId, productId, ct);

        if (product.Price != request.Price || product.PromotionalPrice != request.PromotionalPrice)
        {
            db.AuditLogs.Add(new Domain.Audit.AuditLog
            {
                ActorUserId = currentUser.UserId, Action = Domain.Audit.AuditActions.PriceChanged,
                EntityType = nameof(Product), EntityId = product.Id,
                MetadataJson = $"{{\"previousPrice\":{product.Price},\"newPrice\":{request.Price}}}"
            });
        }

        product.MenuCategoryId = request.MenuCategoryId;
        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.ImageUrl = request.ImageUrl;
        product.Price = request.Price;
        product.PromotionalPrice = request.PromotionalPrice;

        foreach (var group in product.OptionGroups)
        {
            db.ProductOptions.RemoveRange(group.Options);
        }
        db.ProductOptionGroups.RemoveRange(product.OptionGroups);
        product.OptionGroups.Clear();

        ApplyOptionGroups(db, product.Id, request.OptionGroups);

        await db.SaveChangesAsync(ct);
        return await GetProductByIdAsync(establishmentId, productId, ct);
    }

    public async Task<ProductResponse> SetProductAvailabilityAsync(Guid establishmentId, Guid productId, UpdateProductAvailabilityRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        var product = await LoadProductAsync(establishmentId, productId, ct);
        product.IsAvailable = request.IsAvailable;
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    private static void ApplyOptionGroups(IAppDbContext db, Guid productId, List<ProductOptionGroupRequest> groups)
    {
        foreach (var groupRequest in groups)
        {
            var group = new ProductOptionGroup
            {
                ProductId = productId,
                Name = groupRequest.Name.Trim(),
                Required = groupRequest.Required,
                Min = groupRequest.Min,
                Max = groupRequest.Max
            };
            db.ProductOptionGroups.Add(group);

            foreach (var optionRequest in groupRequest.Options)
            {
                db.ProductOptions.Add(new ProductOption
                {
                    ProductOptionGroupId = group.Id,
                    Name = optionRequest.Name.Trim(),
                    PriceDelta = optionRequest.PriceDelta,
                    IsAvailable = optionRequest.IsAvailable
                });
            }
        }
    }

    private async Task<Product> LoadProductAsync(Guid establishmentId, Guid productId, CancellationToken ct) =>
        await db.Products
            .Include(p => p.OptionGroups).ThenInclude(g => g.Options)
            .SingleOrDefaultAsync(p => p.Id == productId && p.EstablishmentId == establishmentId, ct)
        ?? throw new NotFoundException("PRODUCT_NOT_FOUND", "Produto não encontrado.");

    private async Task EnsureMenuCategoryBelongsAsync(Guid establishmentId, Guid menuCategoryId, CancellationToken ct)
    {
        if (!await db.MenuCategories.AnyAsync(c => c.Id == menuCategoryId && c.EstablishmentId == establishmentId, ct))
        {
            throw new NotFoundException("MENU_CATEGORY_NOT_FOUND", "Categoria de cardápio não encontrada.");
        }
    }


    private static ProductResponse ToResponse(Product p) => new(
        p.Id, p.EstablishmentId, p.MenuCategoryId, p.Name, p.Description, p.ImageUrl, p.Price, p.PromotionalPrice,
        p.StockMode.ToString(), p.StockQuantity, p.IsAvailable,
        p.OptionGroups.Select(g => new ProductOptionGroupResponse(
            g.Id, g.Name, g.Required, g.Min, g.Max,
            g.Options.Select(o => new ProductOptionResponse(o.Id, o.Name, o.PriceDelta, o.IsAvailable)).ToList()
        )).ToList());
}
