using LibraryApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Services;

internal static class QueryExtensions
{
    public static async Task<PagedResult<T>> PageAsync<T>(this IQueryable<T> source, PageQuery page, CancellationToken ct)
    {
        var count = await source.CountAsync(ct);
        var offset = (int)Math.Min((long)(page.Page - 1) * page.PageSize, int.MaxValue);
        var items = await source.Skip(offset).Take(page.PageSize).ToListAsync(ct);
        return new(items, count, page.Page, page.PageSize);
    }
}
