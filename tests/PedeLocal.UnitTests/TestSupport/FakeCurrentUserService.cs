using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.UnitTests.TestSupport;

public class FakeCurrentUserService(Guid? userId = null, params string[] roles) : ICurrentUserService
{
    public Guid? UserId { get; set; } = userId;
    public HashSet<string> Roles { get; } = new(roles);

    public bool IsInRole(string role) => Roles.Contains(role);

    public string? IpAddress => "127.0.0.1";
    public string? UserAgent => "unit-tests";
}
