using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Addresses;
using PedeLocal.Domain.Addresses;

namespace PedeLocal.Application.Addresses;

public class AddressService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<List<AddressResponse>> GetMineAsync(CancellationToken ct)
    {
        var userId = RequireUserId();
        return await db.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => ToResponse(a))
            .ToListAsync(ct);
    }

    public async Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken ct)
    {
        var userId = RequireUserId();

        if (!await db.Cities.AnyAsync(c => c.Id == request.CityId && c.IsActive, ct))
        {
            throw new NotFoundException("CITY_NOT_FOUND", "Cidade não encontrada ou inativa.");
        }

        if (!Enum.TryParse<AddressType>(request.Type, ignoreCase: true, out var type))
        {
            throw new AppException("INVALID_ADDRESS_TYPE", "Tipo de endereço inválido.");
        }

        var address = new Address
        {
            CityId = request.CityId,
            UserId = userId,
            Type = type,
            Label = request.Label,
            Street = request.Street,
            Number = request.Number,
            District = request.District,
            Complement = request.Complement,
            Reference = request.Reference,
            ZipCode = request.ZipCode,
            PropertyName = request.PropertyName,
            Road = request.Road,
            Kilometer = request.Kilometer,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            GooglePlaceId = request.GooglePlaceId
        };
        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);
        return ToResponse(address);
    }

    public async Task<AddressResponse> UpdateAsync(Guid id, UpdateAddressRequest request, CancellationToken ct)
    {
        var address = await GetOwnedOrThrowAsync(id, ct);

        if (!Enum.TryParse<AddressType>(request.Type, ignoreCase: true, out var type))
        {
            throw new AppException("INVALID_ADDRESS_TYPE", "Tipo de endereço inválido.");
        }

        address.Type = type;
        address.Label = request.Label;
        address.Street = request.Street;
        address.Number = request.Number;
        address.District = request.District;
        address.Complement = request.Complement;
        address.Reference = request.Reference;
        address.ZipCode = request.ZipCode;
        address.PropertyName = request.PropertyName;
        address.Road = request.Road;
        address.Kilometer = request.Kilometer;
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;
        address.GooglePlaceId = request.GooglePlaceId;

        await db.SaveChangesAsync(ct);
        return ToResponse(address);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var address = await GetOwnedOrThrowAsync(id, ct);
        db.Addresses.Remove(address);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Address> GetOwnedOrThrowAsync(Guid id, CancellationToken ct)
    {
        var userId = RequireUserId();
        var address = await db.Addresses.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("ADDRESS_NOT_FOUND", "Endereço não encontrado.");

        if (address.UserId != userId)
        {
            throw new ForbiddenException("ADDRESS_ACCESS_DENIED", "Você não tem acesso a este endereço.");
        }

        return address;
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");

    private static AddressResponse ToResponse(Address a) => new(
        a.Id, a.CityId, a.Type.ToString(), a.Label, a.Street, a.Number, a.District, a.Complement, a.Reference,
        a.ZipCode, a.PropertyName, a.Road, a.Kilometer, a.Latitude, a.Longitude, a.GooglePlaceId);
}
