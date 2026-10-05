using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Models;

namespace OpenParking.Infrastructure.Extensions;

/// <summary>
/// IQueryable extension to apply skip/take pagination and return a typed PagedResult.
/// Lives in Infrastructure (not Core) because it depends on EF Core's ToListAsync.
/// Usage: await db.Bookings.Where(...).ToPagedResultAsync(query);
/// </summary>
public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginatedQuery q,
        CancellationToken ct = default)
    {
        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip(q.Skip)
            .Take(q.Take)
            .ToListAsync(ct);

        return PagedResult<T>.Create(items, (int)total, q.Page, q.Take);
    }
}
