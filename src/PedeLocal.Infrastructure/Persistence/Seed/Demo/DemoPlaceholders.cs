namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

/// <summary>Well-known placeholder ids for the handful of references the pure planner cannot
/// resolve to a real database id itself (City, EstablishmentCategory rows are looked up/created by
/// <c>DbSeeder</c>/a previous <c>DevelopmentDataSeeder</c> run with a non-deterministic
/// <c>Guid.NewGuid()</c>; the Super Admin actor is the one <c>DevelopmentDataSeeder</c> user this
/// seeder reuses rather than recreates). Both the planner (when building entities) and
/// <c>DemoDataSeeder</c> (when resolving the real id and remapping) call the SAME functions here,
/// so no dictionary needs to travel inside <see cref="DemoDataPlan"/> — the seeder just recomputes
/// the identical placeholder id and knows exactly which entity fields to substitute.</summary>
public static class DemoPlaceholders
{
    public static Guid City(DemoSeedProfile profile, int cityIndex) =>
        DemoIdFactory.For(profile, "city-placeholder", cityIndex);

    public static Guid Category(DemoSeedProfile profile, string categoryName) =>
        DemoIdFactory.For(profile, "category-placeholder", categoryName);

    public static Guid Role(DemoSeedProfile profile, string roleName) =>
        DemoIdFactory.For(profile, "role-placeholder", roleName);

    public static Guid SuperAdmin(DemoSeedProfile profile) =>
        DemoIdFactory.For(profile, "actor-placeholder", "super-admin");
}
