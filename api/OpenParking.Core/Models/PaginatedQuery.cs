namespace OpenParking.Core.Models;

public class PaginatedQuery
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : Math.Max(1, value);
    }

    public string? Search { get; set; }
    public string? SortBy { get; set; }

    /// <summary>Sort direction: "asc" or "desc". Defaults to "desc".</summary>
    public string SortDir { get; set; } = "desc";

    // Legacy compat — keep SortDescending as a two-way alias
    public bool SortDescending
    {
        get => SortDir == "desc";
        set => SortDir = value ? "desc" : "asc";
    }

    /// <summary>Number of records to skip (for EF .Skip()).</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>Number of records to take (for EF .Take()). Alias for PageSize.</summary>
    public int Take => PageSize;
}
