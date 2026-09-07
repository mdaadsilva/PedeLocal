using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Infrastructure.RealTime;

public static class HubGroups
{
    public static string Customer(Guid userId) => $"customer:{userId}";
    public static string Establishment(Guid establishmentId) => $"establishment:{establishmentId}";
    public static string CityDrivers(Guid cityId) => $"city-drivers:{cityId}";
    public static string Driver(Guid driverId) => $"driver:{driverId}";
}

/// <summary>Real-time hub for order/delivery events (docs section 56). Group membership is
/// derived server-side from the authenticated user's own data — clients never choose which
/// group to join, so a customer can't subscribe to another establishment's order feed.</summary>
[Authorize]
public class PedeLocalHub(IAppDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Customer(userId));

            var establishmentIds = await db.EstablishmentUsers
                .Where(eu => eu.UserId == userId)
                .Select(eu => eu.EstablishmentId)
                .ToListAsync();
            foreach (var establishmentId in establishmentIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Establishment(establishmentId));
            }

            var driver = await db.Drivers.SingleOrDefaultAsync(d => d.UserId == userId);
            if (driver is not null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Driver(driver.Id));
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.CityDrivers(driver.CityId));
            }
        }

        await base.OnConnectedAsync();
    }
}
