namespace PedeLocal.Application.Common.Interfaces;

public interface IRouteDistanceService
{
    /// <summary>Distance in kilometers between two coordinates. Implementations may call an
    /// external routing API; the default is a straight-line (haversine) estimate.</summary>
    decimal CalculateDistanceKm(decimal originLatitude, decimal originLongitude, decimal destinationLatitude, decimal destinationLongitude);
}
