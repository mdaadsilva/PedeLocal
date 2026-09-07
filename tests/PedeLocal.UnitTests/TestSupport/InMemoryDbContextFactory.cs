using Microsoft.EntityFrameworkCore;
using PedeLocal.Domain.Identity;
using PedeLocal.Infrastructure.Persistence;

namespace PedeLocal.UnitTests.TestSupport;

public static class InMemoryDbContextFactory
{
    public static PedeLocalDbContext Create()
    {
        var db = CreateAdditionalContext(Guid.NewGuid().ToString());
        foreach (var name in RoleNames.All)
        {
            db.Roles.Add(new Role { Name = name });
        }
        db.SaveChanges();

        return db;
    }

    /// <summary>Second (or third...) DbContext instance pointed at the SAME named InMemory
    /// database as an existing one — for tests that need two independent change trackers over
    /// shared data, e.g. simulating a concurrent-write race between two requests.</summary>
    public static PedeLocalDbContext CreateAdditionalContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<PedeLocalDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new PedeLocalDbContext(options);
    }
}
