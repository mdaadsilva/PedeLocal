using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Application.Identity;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Establishments;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Application.Establishments;

public class EstablishmentService(IAppDbContext db, IPasswordHasher passwordHasher)
{
    public async Task<List<EstablishmentCategoryResponse>> GetCategoriesAsync(CancellationToken ct) =>
        await db.EstablishmentCategories
            .OrderBy(c => c.Name)
            .Select(c => new EstablishmentCategoryResponse(c.Id, c.Name))
            .ToListAsync(ct);

    public async Task<PagedResult<EstablishmentResponse>> GetAllAsync(PageRequest request, CancellationToken ct) =>
        await db.Establishments
            .OrderBy(e => e.Name)
            .Select(e => ToResponse(e))
            .ToPagedResultAsync(request, ct);

    public async Task<EstablishmentResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var establishment = await GetOrThrowAsync(id, ct);
        return ToResponse(establishment);
    }

    /// <summary>Customer-facing search: only establishments in the city that are visible and not suspended.</summary>
    public async Task<PagedResult<EstablishmentResponse>> SearchForCustomersAsync(Guid cityId, PageRequest request, CancellationToken ct) =>
        await PublicEstablishmentsQuery()
            .Where(e => e.CityId == cityId)
            .OrderBy(e => e.Name)
            .Select(e => ToResponse(e))
            .ToPagedResultAsync(request, ct);

    public async Task<EstablishmentResponse> GetForCustomerAsync(Guid id, CancellationToken ct)
    {
        var establishment = await PublicEstablishmentsQuery().SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("ESTABLISHMENT_NOT_FOUND", "Estabelecimento não encontrado.");
        return ToResponse(establishment);
    }

    private IQueryable<Establishment> PublicEstablishmentsQuery() =>
        from e in db.Establishments
        join s in db.EstablishmentSettings on e.Id equals s.EstablishmentId
        where s.IsVisible
              && (e.CommercialStatus == CommercialStatus.Active || e.CommercialStatus == CommercialStatus.OverdueGrace)
        select e;

    public async Task<EstablishmentResponse> CreateAsync(CreateEstablishmentRequest request, CancellationToken ct)
    {
        if (!await db.Cities.AnyAsync(c => c.Id == request.CityId && c.IsActive, ct))
        {
            throw new NotFoundException("CITY_NOT_FOUND", "Cidade não encontrada ou inativa.");
        }

        if (!await db.EstablishmentCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new NotFoundException("CATEGORY_NOT_FOUND", "Categoria de estabelecimento não encontrada.");
        }

        var normalizedAdminEmail = request.AdminEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == normalizedAdminEmail, ct))
        {
            throw new ConflictException("EMAIL_ALREADY_REGISTERED", "Este e-mail já está cadastrado.");
        }

        var address = new Address
        {
            CityId = request.CityId,
            Type = AddressType.Urban,
            Street = request.Street,
            Number = request.Number,
            District = request.District,
            Complement = request.Complement,
            ZipCode = request.ZipCode,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };
        db.Addresses.Add(address);

        var slug = await GenerateUniqueSlugAsync(request.Name, ct);
        var establishment = new Establishment
        {
            CityId = request.CityId,
            Name = request.Name.Trim(),
            Slug = slug,
            Document = request.Document,
            Phone = request.Phone,
            Email = request.Email.Trim().ToLowerInvariant(),
            CategoryId = request.CategoryId,
            AddressId = address.Id,
            OperationalStatus = OperationalStatus.ClosedManual,
            CommercialStatus = CommercialStatus.Active
        };
        db.Establishments.Add(establishment);

        var adminRole = await db.Roles.SingleOrDefaultAsync(r => r.Name == RoleNames.EstablishmentAdmin, ct)
            ?? throw new InvalidOperationException($"Role '{RoleNames.EstablishmentAdmin}' is not seeded.");

        var adminUser = new User
        {
            Name = request.AdminName,
            Email = normalizedAdminEmail,
            PhoneNumber = request.AdminPhone,
            PasswordHash = passwordHasher.Hash(request.AdminPassword)
        };
        adminUser.UserRoles.Add(new UserRole { User = adminUser, Role = adminRole, RoleId = adminRole.Id, UserId = adminUser.Id });
        db.Users.Add(adminUser);

        db.EstablishmentUsers.Add(new EstablishmentUser
        {
            EstablishmentId = establishment.Id,
            UserId = adminUser.Id,
            StaffRole = RoleNames.EstablishmentAdmin
        });

        db.EstablishmentSettings.Add(new EstablishmentSettings
        {
            EstablishmentId = establishment.Id,
            IsVisible = true,
            AcceptsNewOrders = true
        });

        await db.SaveChangesAsync(ct);
        return ToResponse(establishment);
    }

    public async Task<EstablishmentResponse> UpdateAsync(Guid id, UpdateEstablishmentRequest request, CancellationToken ct)
    {
        var establishment = await GetOrThrowAsync(id, ct);

        if (!await db.EstablishmentCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new NotFoundException("CATEGORY_NOT_FOUND", "Categoria de estabelecimento não encontrada.");
        }

        establishment.Name = request.Name.Trim();
        establishment.Phone = request.Phone;
        establishment.Email = request.Email.Trim().ToLowerInvariant();
        establishment.CategoryId = request.CategoryId;

        var address = await db.Addresses.SingleOrDefaultAsync(a => a.Id == establishment.AddressId, ct)
            ?? throw new NotFoundException("ADDRESS_NOT_FOUND", "Endereço do estabelecimento não encontrado.");
        address.Street = request.Street;
        address.Number = request.Number;
        address.District = request.District;
        address.Complement = request.Complement;
        address.ZipCode = request.ZipCode;
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;

        await db.SaveChangesAsync(ct);
        return ToResponse(establishment);
    }

    public async Task<EstablishmentResponse> SetStatusAsync(Guid id, UpdateEstablishmentStatusRequest request, CancellationToken ct)
    {
        var establishment = await GetOrThrowAsync(id, ct);

        if (request.OperationalStatus is not null)
        {
            if (!Enum.TryParse<OperationalStatus>(request.OperationalStatus, ignoreCase: true, out var operationalStatus))
            {
                throw new AppException("INVALID_OPERATIONAL_STATUS", "Status operacional inválido.");
            }
            establishment.OperationalStatus = operationalStatus;
        }

        if (request.CommercialStatus is not null)
        {
            if (!Enum.TryParse<CommercialStatus>(request.CommercialStatus, ignoreCase: true, out var commercialStatus))
            {
                throw new AppException("INVALID_COMMERCIAL_STATUS", "Status comercial inválido.");
            }
            establishment.CommercialStatus = commercialStatus;
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(establishment);
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = SlugGenerator.Slugify(name);
        var slug = baseSlug;
        var suffix = 1;
        while (await db.Establishments.AnyAsync(e => e.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{++suffix}";
        }
        return slug;
    }

    private async Task<Establishment> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await db.Establishments.SingleOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new NotFoundException("ESTABLISHMENT_NOT_FOUND", "Estabelecimento não encontrado.");

    private static EstablishmentResponse ToResponse(Establishment e) => new(
        e.Id, e.CityId, e.Name, e.Slug, e.Document, e.Phone, e.Email, e.CategoryId, e.AddressId,
        e.OperationalStatus.ToString(), e.CommercialStatus.ToString(), e.CreatedAtUtc);
}
