using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Cities;
using PedeLocal.Domain.Cities;

namespace PedeLocal.Application.Cities;

public class CityService(IAppDbContext db)
{
    public async Task<List<CityResponse>> GetActiveAsync(CancellationToken ct) =>
        await db.Cities
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => ToResponse(c))
            .ToListAsync(ct);

    public async Task<List<CityResponse>> GetAllAsync(CancellationToken ct) =>
        await db.Cities
            .OrderBy(c => c.Name)
            .Select(c => ToResponse(c))
            .ToListAsync(ct);

    public async Task<CityResponse> CreateAsync(CreateCityRequest request, CancellationToken ct)
    {
        var city = new City { Name = request.Name.Trim(), State = request.State.Trim().ToUpperInvariant() };
        db.Cities.Add(city);
        await db.SaveChangesAsync(ct);
        return ToResponse(city);
    }

    public async Task<CityResponse> UpdateAsync(Guid id, UpdateCityRequest request, CancellationToken ct)
    {
        var city = await GetOrThrowAsync(id, ct);
        city.Name = request.Name.Trim();
        city.State = request.State.Trim().ToUpperInvariant();
        await db.SaveChangesAsync(ct);
        return ToResponse(city);
    }

    public async Task<CityResponse> SetStatusAsync(Guid id, UpdateCityStatusRequest request, CancellationToken ct)
    {
        var city = await GetOrThrowAsync(id, ct);
        city.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return ToResponse(city);
    }

    private async Task<City> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await db.Cities.SingleOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("CITY_NOT_FOUND", "Cidade não encontrada.");

    private static CityResponse ToResponse(City c) => new(c.Id, c.Name, c.State, c.IsActive);
}
