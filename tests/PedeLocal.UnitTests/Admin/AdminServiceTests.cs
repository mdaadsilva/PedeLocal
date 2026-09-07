using PedeLocal.Application.Admin;
using PedeLocal.Contracts.Admin;
using PedeLocal.Contracts.Common;
using PedeLocal.Domain.Identity;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Admin;

public class AdminServiceTests
{
    [Fact]
    public async Task GetCustomers_OnlyReturnsUsersWithCustomerRole()
    {
        var db = InMemoryDbContextFactory.Create();
        var customerRole = db.Roles.Single(r => r.Name == RoleNames.Customer);
        var driverRole = db.Roles.Single(r => r.Name == RoleNames.Driver);

        var customer = new User { Name = "Cliente", Email = "cliente@example.com", PhoneNumber = "1", PasswordHash = "x" };
        customer.UserRoles.Add(new UserRole { User = customer, Role = customerRole, UserId = customer.Id, RoleId = customerRole.Id });
        var driver = new User { Name = "Motoboy", Email = "motoboy@example.com", PhoneNumber = "1", PasswordHash = "x" };
        driver.UserRoles.Add(new UserRole { User = driver, Role = driverRole, UserId = driver.Id, RoleId = driverRole.Id });
        db.Users.AddRange(customer, driver);
        await db.SaveChangesAsync();

        var sut = new AdminService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));
        var result = await sut.GetCustomersAsync(new PageRequest(), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("cliente@example.com", result.Items[0].Email);
    }

    [Fact]
    public async Task SetFeature_CreatesAndLogsAudit()
    {
        var db = InMemoryDbContextFactory.Create();
        var establishmentId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var sut = new AdminService(db, new FakeCurrentUserService(adminUserId, RoleNames.SuperAdmin));

        var response = await sut.SetFeatureAsync(establishmentId, "WHOLESALE_MODE", new SetFeatureRequest(true, null), CancellationToken.None);

        Assert.True(response.Enabled);
        Assert.Contains(db.AuditLogs, a => a.Action == Domain.Audit.AuditActions.FeatureChanged);
    }

    [Fact]
    public async Task SetFeature_Toggle_UpdatesExistingRow_DoesNotDuplicate()
    {
        var db = InMemoryDbContextFactory.Create();
        var establishmentId = Guid.NewGuid();
        var sut = new AdminService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        await sut.SetFeatureAsync(establishmentId, "WHOLESALE_MODE", new SetFeatureRequest(true, null), CancellationToken.None);
        await sut.SetFeatureAsync(establishmentId, "WHOLESALE_MODE", new SetFeatureRequest(false, null), CancellationToken.None);

        var features = await sut.GetFeaturesAsync(establishmentId, CancellationToken.None);
        Assert.Single(features);
        Assert.False(features[0].Enabled);
    }
}
