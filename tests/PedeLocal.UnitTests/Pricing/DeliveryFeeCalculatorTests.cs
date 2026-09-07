using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Pricing;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Pricing;
using Xunit;

namespace PedeLocal.UnitTests.Pricing;

public class DeliveryFeeCalculatorTests
{
    [Fact]
    public void PerKm_BelowMinimum_ChargesMinimumFee()
    {
        // Doc example: 6km, R$3/km, minimum R$20 -> should charge R$20 (18 < 20).
        var settings = new DeliveryPricingSettings
        {
            UrbanMode = DeliveryFeeMode.PerKm,
            UrbanPricePerKm = 3m,
            UrbanMinFee = 20m,
            MaxDistanceKm = 15
        };

        var quote = DeliveryFeeCalculator.Calculate(settings, AddressType.Urban, 6m);

        Assert.Equal(20m, quote.Fee);
    }

    [Fact]
    public void PerKm_AboveMinimum_ChargesDistanceTimesRate()
    {
        var settings = new DeliveryPricingSettings
        {
            UrbanMode = DeliveryFeeMode.PerKm,
            UrbanPricePerKm = 3m,
            UrbanMinFee = 10m,
            MaxDistanceKm = 15
        };

        var quote = DeliveryFeeCalculator.Calculate(settings, AddressType.Urban, 10m);

        Assert.Equal(30m, quote.Fee);
    }

    [Fact]
    public void Fixed_IgnoresDistance()
    {
        var settings = new DeliveryPricingSettings
        {
            UrbanMode = DeliveryFeeMode.Fixed,
            UrbanFixedFee = 8m,
            MaxDistanceKm = 15
        };

        var quote = DeliveryFeeCalculator.Calculate(settings, AddressType.Urban, 12m);

        Assert.Equal(8m, quote.Fee);
    }

    [Fact]
    public void MixedModel_UsesDifferentModePerAddressType()
    {
        var settings = new DeliveryPricingSettings
        {
            UrbanMode = DeliveryFeeMode.Fixed,
            UrbanFixedFee = 5m,
            RuralMode = DeliveryFeeMode.PerKm,
            RuralPricePerKm = 2m,
            RuralMinFee = 15m,
            MaxDistanceKm = 30
        };

        var urbanQuote = DeliveryFeeCalculator.Calculate(settings, AddressType.Urban, 3m);
        var ruralQuote = DeliveryFeeCalculator.Calculate(settings, AddressType.Rural, 10m);

        Assert.Equal(5m, urbanQuote.Fee);
        Assert.Equal(20m, ruralQuote.Fee);
    }

    [Fact]
    public void DistanceAboveMax_ThrowsDeliveryNotAvailable()
    {
        var settings = new DeliveryPricingSettings { MaxDistanceKm = 10 };

        var ex = Assert.Throws<AppException>(() => DeliveryFeeCalculator.Calculate(settings, AddressType.Urban, 11m));
        Assert.Equal("DELIVERY_NOT_AVAILABLE", ex.Code);
    }
}
