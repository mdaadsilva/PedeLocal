namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

/// <summary>Which demo-data volume to generate. See docs/DEMO_DATA.md. DEMO is the recommended
/// default — SMALL is for fast local dev loops, LARGE is for manual visual/performance testing
/// only (never run automatically).</summary>
public enum DemoSeedProfile
{
    Small,
    Demo,
    Large
}

/// <summary>Per-profile target volumes. These are targets the planner aims for, not hard
/// guarantees — actual counts vary slightly with per-establishment randomization (product count by
/// size, order density by day-of-week/time-of-day weighting) and are reported by the CLI summary
/// after a real run. See docs/DEMO_DATA.md's volume table for the numbers this was calibrated
/// against.</summary>
public sealed record DemoSeedOptions(
    /// <summary>How many of the 3 launch cities to use. SMALL uses just Guaimbê (index 0); DEMO/LARGE use all 3.</summary>
    int CityCount,
    /// <summary>Target establishment count per city, in city order (Guaimbê, Júlio Mesquita, Getulina).</summary>
    int[] EstablishmentsPerCity,
    /// <summary>Target filler customers per city (on top of the 1 predictable customer.<cidade>@pedelocal.dev per city).</summary>
    int FillerCustomersPerCity,
    /// <summary>Target filler drivers per city (on top of the 1 predictable driver.<cidade>@pedelocal.dev per city).</summary>
    int FillerDriversPerCity,
    /// <summary>How many days of order history to generate, ending today (inclusive).</summary>
    int OrderHistoryDays,
    /// <summary>Baseline expected orders/establishment/day at an off-peak hour on a normal weekday — scaled up by
    /// the peak-hour/peak-day multipliers in DemoDataPlanner for lunch/afternoon/evening and Friday/Saturday.</summary>
    decimal BaseOrdersPerEstablishmentPerDay)
{
    public static DemoSeedOptions For(DemoSeedProfile profile) => profile switch
    {
        DemoSeedProfile.Small => new DemoSeedOptions(
            CityCount: 1,
            EstablishmentsPerCity: [5],
            FillerCustomersPerCity: 7,
            FillerDriversPerCity: 2,
            OrderHistoryDays: 3,
            BaseOrdersPerEstablishmentPerDay: 1.2m),
        DemoSeedProfile.Demo => new DemoSeedOptions(
            CityCount: 3,
            EstablishmentsPerCity: [30, 10, 20],
            FillerCustomersPerCity: 20,
            FillerDriversPerCity: 7,
            OrderHistoryDays: 30,
            BaseOrdersPerEstablishmentPerDay: 1.1m),
        DemoSeedProfile.Large => new DemoSeedOptions(
            CityCount: 3,
            EstablishmentsPerCity: [60, 20, 40],
            FillerCustomersPerCity: 45,
            FillerDriversPerCity: 14,
            OrderHistoryDays: 30,
            BaseOrdersPerEstablishmentPerDay: 1.3m),
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}
