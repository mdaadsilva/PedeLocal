using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Application.Common;

public class DriverAccessGuard(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<Domain.Drivers.Driver> GetOwnDriverAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");
        return await db.Drivers.SingleOrDefaultAsync(d => d.UserId == userId, ct)
            ?? throw new NotFoundException("DRIVER_PROFILE_NOT_FOUND", "Perfil de entregador não encontrado.");
    }
}
