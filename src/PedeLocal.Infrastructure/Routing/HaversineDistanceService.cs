using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Infrastructure.Routing;

/// <summary>Great-circle distance estimate. Stands in for a real routing API (Google Maps
/// Distance Matrix, etc.) until one is configured — see docs/DECISIONS.md and section 58.</summary>
public class HaversineDistanceService : IRouteDistanceService
{
    private const double EarthRadiusKm = 6371.0;

    public decimal CalculateDistanceKm(decimal originLatitude, decimal originLongitude, decimal destinationLatitude, decimal destinationLongitude)
    {
        var lat1 = ToRadians((double)originLatitude);
        var lat2 = ToRadians((double)destinationLatitude);
        var deltaLat = ToRadians((double)(destinationLatitude - originLatitude));
        var deltaLon = ToRadians((double)(destinationLongitude - originLongitude));

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        var distanceKm = EarthRadiusKm * c;
        return (decimal)Math.Round(distanceKm, 3);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
