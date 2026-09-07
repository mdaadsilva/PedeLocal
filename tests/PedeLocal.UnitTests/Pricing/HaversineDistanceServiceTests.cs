using PedeLocal.Infrastructure.Routing;
using Xunit;

namespace PedeLocal.UnitTests.Pricing;

public class HaversineDistanceServiceTests
{
    [Fact]
    public void SamePoint_ReturnsZero()
    {
        var sut = new HaversineDistanceService();
        var distance = sut.CalculateDistanceKm(-21.8m, -49.9m, -21.8m, -49.9m);
        Assert.Equal(0m, distance);
    }

    [Fact]
    public void KnownPoints_ReturnsApproximateDistance()
    {
        // São Paulo (-23.5505, -46.6333) to Campinas (-22.9099, -47.0626) is ~90km apart.
        var sut = new HaversineDistanceService();
        var distance = sut.CalculateDistanceKm(-23.5505m, -46.6333m, -22.9099m, -47.0626m);
        Assert.InRange(distance, 80m, 100m);
    }
}
