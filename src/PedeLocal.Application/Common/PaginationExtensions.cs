using Microsoft.EntityFrameworkCore;
using PedeLocal.Contracts.Common;

namespace PedeLocal.Application.Common;

public static class PaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageRequest request, CancellationToken ct)
    {
        var page = request.SafePage;
        var pageSize = request.SafePageSize;

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<T>(items, page, pageSize, total);
    }
}
