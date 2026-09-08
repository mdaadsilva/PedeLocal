using Microsoft.EntityFrameworkCore;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Infrastructure.Persistence.Seed;

/// <summary>Idempotent seeding of static reference data the app cannot function without.</summary>
public static class DbSeeder
{
    private static readonly string[] DefaultEstablishmentCategories =
    [
        "Mercado", "Lanchonete", "Adega", "Pizzaria", "Padaria", "Açougue",
        // Added alongside the Development-only demo data seeder (docs/DEMO_DATA.md) to cover every
        // establishment type it generates — reference data, seeded in every environment like the
        // categories above, never gated to Development.
        "Supermercado", "Restaurante", "Farmácia", "Conveniência", "Hortifruti", "Hamburgueria"
    ];

    public static async Task SeedRolesAsync(PedeLocalDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Roles.Select(r => r.Name).ToListAsync(ct);
        var missing = RoleNames.All.Except(existing);

        foreach (var name in missing)
        {
            db.Roles.Add(new Role { Name = name });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task SeedEstablishmentCategoriesAsync(PedeLocalDbContext db, CancellationToken ct = default)
    {
        var existing = await db.EstablishmentCategories.Select(c => c.Name).ToListAsync(ct);
        var missing = DefaultEstablishmentCategories.Except(existing);

        foreach (var name in missing)
        {
            db.EstablishmentCategories.Add(new EstablishmentCategory { Name = name });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
