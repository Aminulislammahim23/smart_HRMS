namespace smartHRMS.Application.Common.Models;

/// <summary>
/// One page of a server-side paged list, returned as the <c>data</c> of the standard <see cref="ApiResponse{T}"/>
/// envelope. Pages are 1-based.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; init; } = new();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
