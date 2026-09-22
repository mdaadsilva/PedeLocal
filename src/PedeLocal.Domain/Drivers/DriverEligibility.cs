namespace PedeLocal.Domain.Drivers;

/// <summary>The reason a driver can or cannot work right now. This is a computed read-model, not a
/// persisted column — see the remarks on <see cref="DriverOnboardingStatus"/> for why document
/// expiry in particular is deliberately never stored.</summary>
public enum DriverEligibilityStatus
{
    Eligible,
    PendingDocuments,
    UnderReview,
    Rejected,
    Blocked,
    DocumentExpired
}

/// <summary>Single source of truth for "can this driver go online / accept a delivery right now,
/// and if not, why" — used by both <c>DriverService.GoOnlineAsync</c> and
/// <c>DeliveryService.AcceptAsync</c> so the rule can never drift between the two call sites (it did
/// before: DeliveryService.EnsureDriverCanWork duplicated a subset of DriverService.GoOnlineAsync's
/// checks and neither considered document expiry).</summary>
public static class DriverEligibilityService
{
    public static DriverEligibilityStatus Evaluate(Driver driver, DriverLicense? license, DateTime nowUtc)
    {
        if (driver.Status == DriverStatus.Blocked)
        {
            return DriverEligibilityStatus.Blocked;
        }

        switch (driver.OnboardingStatus)
        {
            case DriverOnboardingStatus.PendingDocuments:
                return DriverEligibilityStatus.PendingDocuments;
            case DriverOnboardingStatus.UnderReview:
                return DriverEligibilityStatus.UnderReview;
            case DriverOnboardingStatus.Rejected:
                return DriverEligibilityStatus.Rejected;
        }

        // Approved from here on. A missing license shouldn't happen (submitting one is required to
        // reach UnderReview), but treat it the same as never having submitted documents rather than
        // throwing, since this is a read path.
        if (license is null || license.ExpiresAt < DateOnly.FromDateTime(nowUtc))
        {
            return license is null ? DriverEligibilityStatus.PendingDocuments : DriverEligibilityStatus.DocumentExpired;
        }

        return DriverEligibilityStatus.Eligible;
    }

    public static bool IsEligible(Driver driver, DriverLicense? license, DateTime nowUtc) =>
        Evaluate(driver, license, nowUtc) == DriverEligibilityStatus.Eligible;
}
