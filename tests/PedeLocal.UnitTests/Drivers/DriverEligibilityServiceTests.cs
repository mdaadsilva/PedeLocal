using PedeLocal.Domain.Drivers;
using Xunit;

namespace PedeLocal.UnitTests.Drivers;

public class DriverEligibilityServiceTests
{
    private static Domain.Drivers.Driver MakeDriver(DriverOnboardingStatus onboarding, DriverStatus status = DriverStatus.Offline) => new()
    {
        UserId = Guid.NewGuid(), CityId = Guid.NewGuid(), Cpf = "1", VehicleType = VehicleType.Motorcycle,
        PixKeyType = PixKeyType.Cpf, PixKey = "1", OnboardingStatus = onboarding, Status = status
    };

    private static DriverLicense ValidLicense(int yearsFromNow = 1) => new()
    {
        DriverId = Guid.NewGuid(), RegistrationNumber = "1", Category = "A",
        ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(yearsFromNow))
    };

    [Fact]
    public void Blocked_TakesPriorityOverOnboardingStatus()
    {
        var driver = MakeDriver(DriverOnboardingStatus.Approved, DriverStatus.Blocked);
        var result = DriverEligibilityService.Evaluate(driver, ValidLicense(), DateTime.UtcNow);
        Assert.Equal(DriverEligibilityStatus.Blocked, result);
    }

    [Theory]
    [InlineData(DriverOnboardingStatus.PendingDocuments, DriverEligibilityStatus.PendingDocuments)]
    [InlineData(DriverOnboardingStatus.UnderReview, DriverEligibilityStatus.UnderReview)]
    [InlineData(DriverOnboardingStatus.Rejected, DriverEligibilityStatus.Rejected)]
    public void NonApproved_MapsDirectlyToMatchingEligibility(DriverOnboardingStatus onboarding, DriverEligibilityStatus expected)
    {
        var driver = MakeDriver(onboarding);
        var result = DriverEligibilityService.Evaluate(driver, null, DateTime.UtcNow);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Approved_WithValidLicense_IsEligible()
    {
        var driver = MakeDriver(DriverOnboardingStatus.Approved);
        var result = DriverEligibilityService.Evaluate(driver, ValidLicense(), DateTime.UtcNow);
        Assert.Equal(DriverEligibilityStatus.Eligible, result);
    }

    [Fact]
    public void Approved_WithExpiredLicense_IsDocumentExpired()
    {
        var driver = MakeDriver(DriverOnboardingStatus.Approved);
        var expired = ValidLicense(-1);
        var result = DriverEligibilityService.Evaluate(driver, expired, DateTime.UtcNow);
        Assert.Equal(DriverEligibilityStatus.DocumentExpired, result);
    }

    [Fact]
    public void Approved_WithNoLicenseOnFile_IsPendingDocuments()
    {
        var driver = MakeDriver(DriverOnboardingStatus.Approved);
        var result = DriverEligibilityService.Evaluate(driver, null, DateTime.UtcNow);
        Assert.Equal(DriverEligibilityStatus.PendingDocuments, result);
    }

    [Fact]
    public void IsEligible_MatchesEvaluateEqualsEligible()
    {
        var driver = MakeDriver(DriverOnboardingStatus.Approved);
        Assert.True(DriverEligibilityService.IsEligible(driver, ValidLicense(), DateTime.UtcNow));
        Assert.False(DriverEligibilityService.IsEligible(driver, ValidLicense(-1), DateTime.UtcNow));
    }
}
