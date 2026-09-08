using System.Security.Cryptography;
using System.Text;

namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

/// <summary>Deterministic GUID derivation: the same (profile, category, key) always produces the
/// same Guid, on any machine, on any run. This is the whole idempotency/reset strategy for demo
/// data — see docs/DEMO_DATA.md "Como identificar dado demo". No new marker table, no filesystem
/// state: `DemoDataSeeder.SeedAsync` upserts by these ids (skips what already exists),
/// `ResetAsync` recomputes the exact same ids from the same planner and deletes only rows matching
/// them (`WHERE Id IN (...)`, never a blanket delete). Different profiles never collide because the
/// profile name is part of the hashed key.</summary>
public static class DemoIdFactory
{
    /// <summary>Stable across processes/machines — MD5 of a UTF8 string, first 16 bytes as the
    /// Guid. Not used for anything security-sensitive; collision resistance at this data volume
    /// (thousands of keys) is effectively perfect for this purpose.</summary>
    public static Guid For(DemoSeedProfile profile, string category, string key)
    {
        var input = $"pedelocal-demo|{profile}|{category}|{key}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    public static Guid For(DemoSeedProfile profile, string category, int index) =>
        For(profile, category, index.ToString());

    /// <summary>Same idea as <see cref="For(DemoSeedProfile,string,string)"/> but WITHOUT the
    /// profile in the hash — for the handful of entities meant to be the exact same identity no
    /// matter which profile is active: the 9 predictable accounts
    /// (customer/driver/merchant.&lt;cidade&gt;@pedelocal.dev — see docs/DEMO_DATA.md) and their
    /// addresses. Their literal email is fixed by the task regardless of profile, so their id must
    /// be too — otherwise seeding a second profile on top of a first without resetting first would
    /// try to insert a second row with the SAME email and a DIFFERENT id, violating the real unique
    /// index on User.Email. Every other demo entity stays profile-scoped via <see cref="For(DemoSeedProfile,string,string)"/>.</summary>
    public static Guid ForShared(string category, string key)
    {
        var input = $"pedelocal-demo|shared|{category}|{key}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
