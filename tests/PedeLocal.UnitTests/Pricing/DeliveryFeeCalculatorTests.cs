using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Pricing;
using PedeLocal.Domain.Pricing;
using Xunit;

namespace PedeLocal.UnitTests.Pricing;

public class DeliveryFeeCalculatorTests
{
    [Fact]
    public void WithinRadius_ChargesFlatFee_RegardlessOfDistance()
    {
        var settings = new DeliveryPricingSettings { DeliveryFee = 7m, DeliveryRadiusKm = 15 };

        var nearQuote = DeliveryFeeCalculator.Calculate(settings, 1m);
        var farQuote = DeliveryFeeCalculator.Calculate(settings, 14.9m);

        Assert.Equal(7m, nearQuote.Fee);
        Assert.Equal(7m, farQuote.Fee);
    }

    [Fact]
    public void ExactlyAtRadius_IsStillAvailable()
    {
        var settings = new DeliveryPricingSettings { DeliveryFee = 7m, DeliveryRadiusKm = 8 };

        var quote = DeliveryFeeCalculator.Calculate(settings, 8m);

        Assert.Equal(7m, quote.Fee);
    }

    [Fact]
    public void DistanceAboveRadius_ThrowsOutsideDeliveryArea()
    {
        var settings = new DeliveryPricingSettings { DeliveryFee = 7m, DeliveryRadiusKm = 8 };

        var ex = Assert.Throws<AppException>(() => DeliveryFeeCalculator.Calculate(settings, 10m));

        Assert.Equal("OUTSIDE_DELIVERY_AREA", ex.Code);
    }

    [Fact]
    public void Snapshot_CapturesFeeAndDistanceAtCalculationTime()
    {
        var settings = new DeliveryPricingSettings { DeliveryFee = 7m, DeliveryRadiusKm = 15 };

        var quote = DeliveryFeeCalculator.Calculate(settings, 4m);

        Assert.Contains("\"deliveryFee\":7", quote.PricingRuleSnapshotJson);
        Assert.Equal(4m, quote.DistanceKm);
    }
}
