using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Deliveries;
using PedeLocal.Application.Identity;
using PedeLocal.Domain.Deliveries;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

/// <summary>Applies a <see cref="DemoDataPlan"/> (built by the pure <see cref="DemoDataPlanner"/>)
/// to a real <see cref="PedeLocalDbContext"/> — Development only, see docs/DEMO_DATA.md.
///
/// SAFETY: this is the second of two independent environment guards (the first is in
/// <c>Program.cs</c>, before the host is even fully built) — this one exists so calling this class
/// from anywhere else, now or in the future, can never bypass the check.
///
/// IDEMPOTENCY / RESET: every entity's id comes from <see cref="DemoIdFactory"/> — deterministic,
/// namespaced by profile. `SeedAsync` inserts only rows that don't already exist by that id (safe
/// to run repeatedly, never duplicates). `ResetAsync` recomputes the exact same plan and deletes
/// only rows matching those ids, staged child-before-parent — never a blanket DELETE, and it can
/// never touch `DevelopmentDataSeeder`'s Super Admin/example data (different id namespace
/// entirely) or shared reference data (City/EstablishmentCategory/Role rows are resolved by NAME,
/// looked up — never created or deleted by this class).</summary>
public static class DemoDataSeeder
{
    public static async Task<DemoSeedResult> SeedAsync(
        PedeLocalDbContext db, IPasswordHasher passwordHasher, ITokenService tokenService, IDeliveryCodeCipher codeCipher,
        DemoSeedProfile profile, string environmentName, CancellationToken ct = default)
    {
        EnsureDevelopment(environmentName);

        var plan = DemoDataPlanner.Plan(profile);
        var refs = await ResolveReferencesAsync(db, profile, ct);
        RemapPlaceholders(plan, refs);

        var sharedHash = passwordHasher.Hash(DevelopmentDataSeeder.DevPassword);
        foreach (var user in plan.Users)
        {
            user.PasswordHash = sharedHash;
        }

        var confirmations = plan.PendingConfirmations.Select(pc => new DeliveryConfirmation
        {
            DeliveryId = pc.DeliveryId,
            CodeHash = tokenService.HashToken(pc.PlainCode),
            EncryptedCode = codeCipher.Encrypt(pc.PlainCode),
            ExpiresAtUtc = pc.ExpiresAtUtc,
            Attempts = pc.Attempts,
            MaxAttempts = pc.MaxAttempts,
            ConfirmedAtUtc = pc.ConfirmedAtUtc
        }).ToList();

        var counts = new Dictionary<string, int>();

        // Stage 1 — cities (special: looked up by NAME, only inserted if truly missing).
        var newCities = 0;
        foreach (var city in plan.Cities)
        {
            if (!refs.CityIdByName.ContainsKey(city.Name))
            {
                db.Cities.Add(city);
                newCities++;
            }
        }
        if (newCities > 0)
        {
            await db.SaveChangesAsync(ct);
        }
        counts["Cities (new)"] = newCities;

        // Stage 2 — people.
        counts["Users"] = await AddMissingAsync(db.Users, plan.Users, u => u.Id, ct);
        counts["Addresses"] = await AddMissingAsync(db.Addresses, plan.Addresses, a => a.Id, ct);
        // UserRole has no single-column id — UserId is a good enough idempotency key here since
        // every demo user is planned with exactly one role.
        await AddMissingAsync(db.UserRoles, plan.UserRoles, ur => ur.UserId, ct);
        await db.SaveChangesAsync(ct);

        // Stage 3 — establishments and everything keyed 1:1 to one.
        counts["Establishments"] = await AddMissingAsync(db.Establishments, plan.Establishments, e => e.Id, ct);
        await AddMissingAsync(db.EstablishmentUsers, plan.EstablishmentUsers, eu => eu.UserId, ct);
        await AddMissingAsync(db.EstablishmentSettings, plan.EstablishmentSettingsList, s => s.EstablishmentId, ct);
        await AddMissingAsync(db.DeliveryPricingSettings, plan.DeliveryPricingSettingsList, p => p.EstablishmentId, ct);
        await AddMissingAsync(db.EstablishmentFeatures, plan.EstablishmentFeatures, f => f.Id, ct);
        counts["Subscriptions"] = await AddMissingAsync(db.EstablishmentSubscriptions, plan.Subscriptions, s => s.Id, ct);
        await AddMissingAsync(db.EstablishmentPayments, plan.Payments, p => p.Id, ct);
        await db.SaveChangesAsync(ct);

        // Stage 4 — catalog.
        await AddMissingAsync(db.MenuCategories, plan.MenuCategories, m => m.Id, ct);
        counts["Products"] = await AddMissingAsync(db.Products, plan.Products, p => p.Id, ct);
        await AddMissingAsync(db.ProductOptionGroups, plan.ProductOptionGroups, g => g.Id, ct);
        await AddMissingAsync(db.ProductOptions, plan.ProductOptions, o => o.Id, ct);
        await db.SaveChangesAsync(ct);

        // Stage 5 — drivers.
        counts["Drivers"] = await AddMissingAsync(db.Drivers, plan.Drivers, d => d.Id, ct);
        await AddMissingAsync(db.DriverLicenses, plan.DriverLicenses, l => l.DriverId, ct);
        await AddMissingAsync(db.DriverVehicles, plan.DriverVehicles, v => v.DriverId, ct);
        await db.SaveChangesAsync(ct);

        // Stage 6 — orders, chunked so a LARGE profile never does one giant SaveChanges.
        counts["Orders"] = await AddMissingChunkedAsync(db, db.Orders, plan.Orders, o => o.Id, ct);
        await AddMissingChunkedAsync(db, db.OrderItems, plan.OrderItems, i => i.Id, ct);
        await AddMissingChunkedAsync(db, db.OrderItemOptions, plan.OrderItemOptions, o => o.Id, ct);
        await AddMissingChunkedAsync(db, db.OrderStatusHistories, plan.OrderStatusHistories, h => h.Id, ct);

        // Stage 7 — deliveries and everything that references an Order (now guaranteed committed).
        counts["Deliveries"] = await AddMissingChunkedAsync(db, db.Deliveries, plan.Deliveries, d => d.Id, ct);
        await AddMissingChunkedAsync(db, db.DeliveryConfirmations, confirmations, c => c.DeliveryId, ct);
        await AddMissingChunkedAsync(db, db.DriverReceivables, plan.DriverReceivables, r => r.Id, ct);
        await AddMissingChunkedAsync(db, db.InventoryMovements, plan.InventoryMovements, m => m.Id, ct);

        return new DemoSeedResult(profile, counts, plan.Credentials, plan.EstablishmentsWithoutSubscription.Count);
    }

    public static async Task ResetAsync(PedeLocalDbContext db, DemoSeedProfile profile, string environmentName, CancellationToken ct = default)
    {
        EnsureDevelopment(environmentName);

        var plan = DemoDataPlanner.Plan(profile);

        // Child-before-parent — the exact reverse of SeedAsync's stage order.
        await DeleteMatchingAsync(db, db.InventoryMovements, m => m.Id, plan.InventoryMovements.Select(m => m.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.DriverReceivables, r => r.Id, plan.DriverReceivables.Select(r => r.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.DeliveryConfirmations, c => c.DeliveryId, plan.PendingConfirmations.Select(c => c.DeliveryId).ToList(), ct);
        await DeleteMatchingAsync(db, db.Deliveries, d => d.Id, plan.Deliveries.Select(d => d.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.OrderStatusHistories, h => h.Id, plan.OrderStatusHistories.Select(h => h.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.OrderItemOptions, o => o.Id, plan.OrderItemOptions.Select(o => o.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.OrderItems, i => i.Id, plan.OrderItems.Select(i => i.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.Orders, o => o.Id, plan.Orders.Select(o => o.Id).ToList(), ct);

        await DeleteMatchingAsync(db, db.DriverLicenses, l => l.DriverId, plan.DriverLicenses.Select(l => l.DriverId).ToList(), ct);
        await DeleteMatchingAsync(db, db.DriverVehicles, v => v.DriverId, plan.DriverVehicles.Select(v => v.DriverId).ToList(), ct);
        await DeleteMatchingAsync(db, db.Drivers, d => d.Id, plan.Drivers.Select(d => d.Id).ToList(), ct);

        await DeleteMatchingAsync(db, db.ProductOptions, o => o.Id, plan.ProductOptions.Select(o => o.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.ProductOptionGroups, g => g.Id, plan.ProductOptionGroups.Select(g => g.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.Products, p => p.Id, plan.Products.Select(p => p.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.MenuCategories, m => m.Id, plan.MenuCategories.Select(m => m.Id).ToList(), ct);

        await DeleteMatchingAsync(db, db.EstablishmentPayments, p => p.Id, plan.Payments.Select(p => p.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.EstablishmentSubscriptions, s => s.Id, plan.Subscriptions.Select(s => s.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.EstablishmentFeatures, f => f.Id, plan.EstablishmentFeatures.Select(f => f.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.DeliveryPricingSettings, p => p.EstablishmentId, plan.DeliveryPricingSettingsList.Select(p => p.EstablishmentId).ToList(), ct);
        await DeleteMatchingAsync(db, db.EstablishmentSettings, s => s.EstablishmentId, plan.EstablishmentSettingsList.Select(s => s.EstablishmentId).ToList(), ct);
        await DeleteMatchingAsync(db, db.EstablishmentUsers, eu => eu.UserId, plan.EstablishmentUsers.Select(eu => eu.UserId).ToList(), ct);
        await DeleteMatchingAsync(db, db.Establishments, e => e.Id, plan.Establishments.Select(e => e.Id).ToList(), ct);

        await DeleteMatchingAsync(db, db.Addresses, a => a.Id, plan.Addresses.Select(a => a.Id).ToList(), ct);
        await DeleteMatchingAsync(db, db.UserRoles, ur => ur.UserId, plan.UserRoles.Select(ur => ur.UserId).ToList(), ct);
        await DeleteMatchingAsync(db, db.Users, u => u.Id, plan.Users.Select(u => u.Id).ToList(), ct);

        // Cities: only ever matches a row THIS seeder itself created (a pre-existing
        // DevelopmentDataSeeder city has a different, non-deterministic real id and simply never
        // appears in this WHERE Id IN (...) — nothing to special-case here).
        await DeleteMatchingAsync(db, db.Cities, c => c.Id, plan.Cities.Select(c => c.Id).ToList(), ct);
    }

    /// <summary>Matches ASP.NET Core's own `Microsoft.Extensions.Hosting.Environments.Development`
    /// constant value exactly — compared as a literal here so this Infrastructure-layer class
    /// doesn't need a hosting-abstractions package reference just for one string constant.</summary>
    public const string DevelopmentEnvironmentName = "Development";

    private static void EnsureDevelopment(string environmentName)
    {
        if (environmentName != DevelopmentEnvironmentName)
        {
            throw new InvalidOperationException(
                $"Demo data seeding/reset only runs in Development (current environment: '{environmentName}'). Refusing.");
        }
    }

    private sealed record ResolvedReferences(
        Dictionary<string, Guid> CityIdByName, Dictionary<string, Guid> CategoryIdByName,
        Dictionary<string, Guid> RoleIdByName, Guid SuperAdminUserId);

    private static async Task<ResolvedReferences> ResolveReferencesAsync(PedeLocalDbContext db, DemoSeedProfile profile, CancellationToken ct)
    {
        var cityIdByName = await db.Cities.ToDictionaryAsync(c => c.Name, c => c.Id, ct);

        var categoryNames = DemoFixtureData.EstablishmentTypes.Select(t => t.CategoryName).Distinct().ToList();
        var categoryIdByName = await db.EstablishmentCategories
            .Where(c => categoryNames.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, c => c.Id, ct);
        var missingCategories = categoryNames.Except(categoryIdByName.Keys).ToList();
        if (missingCategories.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing EstablishmentCategory rows required by the demo seeder: {string.Join(", ", missingCategories)}. " +
                "Run the app once (DbSeeder.SeedEstablishmentCategoriesAsync) before seeding demo data.");
        }

        string[] roleNames = [RoleNames.EstablishmentAdmin, RoleNames.Customer, RoleNames.Driver];
        var roleIdByName = await db.Roles.Where(r => roleNames.Contains(r.Name)).ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var missingRoles = roleNames.Except(roleIdByName.Keys).ToList();
        if (missingRoles.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing Role rows required by the demo seeder: {string.Join(", ", missingRoles)}. " +
                "Run the app once (DbSeeder.SeedRolesAsync) before seeding demo data.");
        }

        var superAdmin = await db.Users.SingleOrDefaultAsync(u => u.Email == DevelopmentDataSeeder.SuperAdminEmail, ct)
            ?? throw new InvalidOperationException(
                $"Super Admin ('{DevelopmentDataSeeder.SuperAdminEmail}') not found. Start the app once in Development " +
                "(DevelopmentDataSeeder must run first) before seeding demo data.");

        _ = profile;
        return new ResolvedReferences(cityIdByName, categoryIdByName, roleIdByName, superAdmin.Id);
    }

    private static void RemapPlaceholders(DemoDataPlan plan, ResolvedReferences refs)
    {
        var remap = new Dictionary<Guid, Guid>();
        for (var i = 0; i < DemoFixtureData.Cities.Length; i++)
        {
            var fx = DemoFixtureData.Cities[i];
            if (refs.CityIdByName.TryGetValue(fx.Name, out var realCityId))
            {
                remap[DemoPlaceholders.City(plan.Profile, i)] = realCityId;
            }
        }
        foreach (var (name, id) in refs.CategoryIdByName)
        {
            remap[DemoPlaceholders.Category(plan.Profile, name)] = id;
        }
        foreach (var (name, id) in refs.RoleIdByName)
        {
            remap[DemoPlaceholders.Role(plan.Profile, name)] = id;
        }
        remap[DemoPlaceholders.SuperAdmin(plan.Profile)] = refs.SuperAdminUserId;

        Guid Remap(Guid id) => remap.TryGetValue(id, out var real) ? real : id;

        foreach (var e in plan.Establishments)
        {
            e.CityId = Remap(e.CityId);
            e.CategoryId = Remap(e.CategoryId);
        }
        foreach (var a in plan.Addresses)
        {
            a.CityId = Remap(a.CityId);
        }
        foreach (var d in plan.Drivers)
        {
            d.CityId = Remap(d.CityId);
        }
        foreach (var o in plan.Orders)
        {
            o.CityId = Remap(o.CityId);
        }
        foreach (var dl in plan.Deliveries)
        {
            dl.CityId = Remap(dl.CityId);
        }
        foreach (var ur in plan.UserRoles)
        {
            ur.RoleId = Remap(ur.RoleId);
        }
        foreach (var p in plan.Payments)
        {
            p.RegisteredByUserId = Remap(p.RegisteredByUserId);
        }
        // Cities themselves: a NEW city (not found by name) keeps its planner-assigned id — that id
        // IS the real id going forward, so no remap needed for plan.Cities' own Id.
    }

    // ---------------------------------------------------------------
    // Generic upsert/delete helpers — every id here is deterministic (DemoIdFactory), so "does a row
    // with this id already exist" is a correct, cheap idempotency check for every entity type.
    // ---------------------------------------------------------------

    private static async Task<int> AddMissingAsync<T>(DbSet<T> set, List<T> entities, Expression<Func<T, Guid>> idSelector, CancellationToken ct)
        where T : class
    {
        if (entities.Count == 0)
        {
            return 0;
        }
        var existingIds = (await set.Select(idSelector).ToListAsync(ct)).ToHashSet();
        var compiled = idSelector.Compile();
        var missing = entities.Where(e => !existingIds.Contains(compiled(e))).ToList();
        if (missing.Count > 0)
        {
            set.AddRange(missing);
        }
        return missing.Count;
    }

    private const int ChunkSize = 300;

    private static async Task<int> AddMissingChunkedAsync<T>(
        PedeLocalDbContext db, DbSet<T> set, List<T> entities, Expression<Func<T, Guid>> idSelector, CancellationToken ct)
        where T : class
    {
        if (entities.Count == 0)
        {
            return 0;
        }
        var existingIds = (await set.Select(idSelector).ToListAsync(ct)).ToHashSet();
        var compiled = idSelector.Compile();
        var missing = entities.Where(e => !existingIds.Contains(compiled(e))).ToList();

        var added = 0;
        foreach (var chunk in missing.Chunk(ChunkSize))
        {
            set.AddRange(chunk);
            await db.SaveChangesAsync(ct);
            added += chunk.Length;
        }
        return added;
    }

    private static async Task DeleteMatchingAsync<T>(
        PedeLocalDbContext db, DbSet<T> set, Expression<Func<T, Guid>> idSelector, List<Guid> ids, CancellationToken ct)
        where T : class
    {
        if (ids.Count == 0)
        {
            return;
        }
        foreach (var chunk in ids.Chunk(ChunkSize))
        {
            var chunkIds = chunk.ToList();
            var predicate = BuildIdInPredicate(idSelector, chunkIds);
            var matches = await set.Where(predicate).ToListAsync(ct);
            if (matches.Count > 0)
            {
                set.RemoveRange(matches);
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static Expression<Func<T, bool>> BuildIdInPredicate<T>(Expression<Func<T, Guid>> idSelector, List<Guid> ids)
    {
        var containsCall = Expression.Call(
            typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)],
            Expression.Constant(ids), idSelector.Body);
        return Expression.Lambda<Func<T, bool>>(containsCall, idSelector.Parameters);
    }
}

/// <summary>Summary the CLI prints after a `--seed-demo` run — see Program.cs.</summary>
public sealed record DemoSeedResult(
    DemoSeedProfile Profile, Dictionary<string, int> Counts, List<DemoCredentialRow> Credentials, int EstablishmentsWithoutSubscription);
