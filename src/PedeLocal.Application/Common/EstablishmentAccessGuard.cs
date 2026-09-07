using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Application.Common;

/// <summary>Ownership check shared by every merchant-facing service: staff of the establishment, or Super Admin.</summary>
public class EstablishmentAccessGuard(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task EnsureAccessAsync(Guid establishmentId, CancellationToken ct)
    {
        if (currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            return;
        }

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");

        var isStaff = await db.EstablishmentUsers
            .AnyAsync(eu => eu.EstablishmentId == establishmentId && eu.UserId == userId, ct);

        if (!isStaff)
        {
            throw new ForbiddenException("ESTABLISHMENT_ACCESS_DENIED", "Você não tem acesso a este estabelecimento.");
        }
    }
}
