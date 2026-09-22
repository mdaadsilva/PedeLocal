using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Auth;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Application.Identity;

public class AuthService(IAppDbContext db, IPasswordHasher passwordHasher, ITokenService tokenService)
{
    public async Task<RegisterCustomerResponse> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct)
    {
        await EnsureEmailIsFreeAsync(request.Email, ct);

        var user = new User
        {
            Name = request.Name,
            Email = request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = request.Phone,
            PasswordHash = passwordHasher.Hash(request.Password)
        };

        await AssignRoleAsync(user, RoleNames.Customer, ct);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return new RegisterCustomerResponse(user.Id, user.Name, user.Email);
    }

    public async Task<RegisterDriverResponse> RegisterDriverAsync(RegisterDriverRequest request, CancellationToken ct)
    {
        await EnsureEmailIsFreeAsync(request.Email, ct);

        if (await db.Drivers.AnyAsync(d => d.Cpf == request.Cpf, ct))
        {
            throw new ConflictException("CPF_ALREADY_REGISTERED", "Este CPF já está cadastrado.");
        }

        if (!await db.Cities.AnyAsync(c => c.Id == request.CityId && c.IsActive, ct))
        {
            throw new NotFoundException("CITY_NOT_FOUND", "Cidade não encontrada ou inativa.");
        }

        if (!Enum.TryParse<VehicleType>(request.VehicleType, ignoreCase: true, out var vehicleType))
        {
            throw new AppException("INVALID_VEHICLE_TYPE", "Tipo de veículo inválido.");
        }

        if (!Enum.TryParse<PixKeyType>(request.PixKeyType, ignoreCase: true, out var pixKeyType))
        {
            throw new AppException("INVALID_PIX_KEY_TYPE", "Tipo de chave PIX inválido.");
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = request.Phone,
            PasswordHash = passwordHasher.Hash(request.Password)
        };

        await AssignRoleAsync(user, RoleNames.Driver, ct);
        db.Users.Add(user);

        var driver = new Driver
        {
            UserId = user.Id,
            CityId = request.CityId,
            Cpf = request.Cpf,
            VehicleType = vehicleType,
            PixKeyType = pixKeyType,
            PixKey = request.PixKey,
            OnboardingStatus = DriverOnboardingStatus.PendingDocuments,
            Status = DriverStatus.Offline
        };
        db.Drivers.Add(driver);

        await db.SaveChangesAsync(ct);

        return new RegisterDriverResponse(user.Id, driver.Id, driver.OnboardingStatus.ToString());
    }

    public async Task<AuthTokensResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAppException("INVALID_CREDENTIALS", "E-mail ou senha inválidos.");
        }

        return await IssueTokensAsync(user, ip, ct);
    }

    public async Task<AuthTokensResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct)
    {
        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var existing = await db.RefreshTokens
            .Include(rt => rt.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (existing is null)
        {
            throw new UnauthorizedAppException("INVALID_REFRESH_TOKEN", "Sessão expirada. Faça login novamente.");
        }

        if (existing.RevokedAtUtc is not null)
        {
            // Reuse of a token that was already rotated away is a strong signal of theft (docs/AUTH_FLOW.md):
            // it means someone else now holds a copy of a token this session already exchanged for a
            // newer one. Revoke the entire live descendant chain — not just this dead token — so
            // whoever is holding the stolen lineage gets logged out too, not just denied this one call.
            await RevokeDescendantChainAsync(existing, ip, ct);
            db.AuditLogs.Add(new Domain.Audit.AuditLog
            {
                ActorUserId = existing.UserId,
                Action = Domain.Audit.AuditActions.RefreshTokenReuseDetected,
                EntityType = nameof(User),
                EntityId = existing.UserId,
                Ip = ip
            });
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedAppException("INVALID_REFRESH_TOKEN", "Sessão expirada. Faça login novamente.");
        }

        if (existing.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("INVALID_REFRESH_TOKEN", "Sessão expirada. Faça login novamente.");
        }

        if (!existing.User.IsActive)
        {
            throw new UnauthorizedAppException("INVALID_REFRESH_TOKEN", "Sessão expirada. Faça login novamente.");
        }

        // Rotation: revoke the presented token and mint a fresh pair.
        existing.RevokedAtUtc = DateTime.UtcNow;
        existing.RevokedByIp = ip;

        var tokens = await IssueTokensAsync(existing.User, ip, ct);
        existing.ReplacedByTokenHash = tokenService.HashToken(tokens.RefreshToken);
        await db.SaveChangesAsync(ct);

        return tokens;
    }

    /// <summary>Walks the rotation chain forward from an already-revoked token via
    /// ReplacedByTokenHash and revokes every still-active descendant — used when reuse of a dead
    /// token is detected, to kill whatever live session descends from it.</summary>
    private async Task RevokeDescendantChainAsync(RefreshToken token, string? ip, CancellationToken ct)
    {
        var nextHash = token.ReplacedByTokenHash;
        var guard = 0; // defensive bound — rotation is a linear chain, this should never loop.
        while (nextHash is not null && guard++ < 100)
        {
            var descendant = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == nextHash, ct);
            if (descendant is null)
            {
                break;
            }
            if (descendant.RevokedAtUtc is null)
            {
                descendant.RevokedAtUtc = DateTime.UtcNow;
                descendant.RevokedByIp = ip;
            }
            nextHash = descendant.ReplacedByTokenHash;
        }
    }

    public async Task LogoutAsync(LogoutRequest request, string? ip, CancellationToken ct)
    {
        var tokenHash = tokenService.HashToken(request.RefreshToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);
        if (existing is null || existing.RevokedAtUtc is not null)
        {
            return;
        }

        existing.RevokedAtUtc = DateTime.UtcNow;
        existing.RevokedByIp = ip;
        await db.SaveChangesAsync(ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

        // Always behave the same way whether or not the e-mail exists, to avoid account enumeration.
        if (user is null)
        {
            return;
        }

        var rawToken = GenerateOpaqueToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashToken(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
        });
        await db.SaveChangesAsync(ct);

        // TODO(notifications): deliver rawToken to the user out-of-band once the Notifications
        // module (Firebase / e-mail) is implemented. Never log or return it here.
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var tokenHash = tokenService.HashToken(request.Token);
        var resetToken = await db.PasswordResetTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (resetToken is null || resetToken.UsedAtUtc is not null || resetToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("INVALID_RESET_TOKEN", "Token de recuperação inválido ou expirado.");
        }

        resetToken.UsedAtUtc = DateTime.UtcNow;
        resetToken.User.PasswordHash = passwordHasher.Hash(request.NewPassword);

        // A password reset invalidates every existing session.
        var activeSessions = await db.RefreshTokens
            .Where(rt => rt.UserId == resetToken.UserId && rt.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<AuthTokensResponse> IssueTokensAsync(User user, string? ip, CancellationToken ct)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshTokenRaw = tokenService.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashToken(refreshTokenRaw),
            ExpiresAtUtc = DateTime.UtcNow.Add(tokenService.RefreshTokenLifetime),
            CreatedByIp = ip
        });
        await db.SaveChangesAsync(ct);

        return new AuthTokensResponse(accessToken.Token, refreshTokenRaw, accessToken.ExpiresAtUtc);
    }

    private async Task EnsureEmailIsFreeAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
        {
            throw new ConflictException("EMAIL_ALREADY_REGISTERED", "Este e-mail já está cadastrado.");
        }
    }

    private async Task AssignRoleAsync(User user, string roleName, CancellationToken ct)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == roleName, ct)
            ?? throw new InvalidOperationException($"Role '{roleName}' is not seeded.");

        user.UserRoles.Add(new UserRole { User = user, Role = role, RoleId = role.Id, UserId = user.Id });
    }

    private static string GenerateOpaqueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
