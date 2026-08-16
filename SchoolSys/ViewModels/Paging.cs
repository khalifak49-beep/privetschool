using Microsoft.EntityFrameworkCore;

namespace SchoolSys.ViewModels;

/// <summary>قائمة مقسّمة إلى صفحات.</summary>
public class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageIndex { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPrevious => PageIndex > 1;
    public bool HasNext => PageIndex < TotalPages;
    public int FirstItemNumber => TotalCount == 0 ? 0 : (PageIndex - 1) * PageSize + 1;
    public int LastItemNumber => Math.Min(PageIndex * PageSize, TotalCount);

    public static async Task<PagedList<T>> CreateAsync(IQueryable<T> query, int pageIndex, int pageSize)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 5, 200);

        var total = await query.CountAsync();
        var items = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedList<T>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public static PagedList<T> Empty(int pageSize = 25) => new() { PageIndex = 1, PageSize = pageSize };
}
