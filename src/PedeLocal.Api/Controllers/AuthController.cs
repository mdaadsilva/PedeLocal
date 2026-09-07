using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PedeLocal.Application.Identity;
using PedeLocal.Contracts.Auth;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register/customer")]
    [ProducesResponseType(typeof(RegisterCustomerResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterCustomer(RegisterCustomerRequest request, CancellationToken ct)
    {
        var response = await authService.RegisterCustomerAsync(request, ct);
        return CreatedAtAction(nameof(RegisterCustomer), new { id = response.UserId }, response);
    }

    [HttpPost("register/driver")]
    [ProducesResponseType(typeof(RegisterDriverResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterDriver(RegisterDriverRequest request, CancellationToken ct)
    {
        var response = await authService.RegisterDriverAsync(request, ct);
        return CreatedAtAction(nameof(RegisterDriver), new { id = response.UserId }, response);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthTokensResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthTokensResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var response = await authService.RefreshAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(response);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await authService.LogoutAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await authService.ResetPasswordAsync(request, ct);
        return NoContent();
    }
}
