using System.Security.Claims;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private HttpContext? Context => httpContextAccessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var sub = Context?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Context?.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsInRole(string role) => Context?.User.IsInRole(role) ?? false;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();
}
