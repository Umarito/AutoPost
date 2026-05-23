namespace Application.Common;

/// <summary>
/// Standard pagination request used by list endpoints across the entire API.
///
/// <para><b>Usage:</b>
/// Passed as a parameter to service methods that return paginated data.
/// Default values ensure a sensible first page is returned even if the client
/// doesn't specify pagination parameters.</para>
/// </summary>
/// <param name="Page">The 1-based page number to retrieve. Default: 1 (first page).</param>
/// <param name="PageSize">The number of items per page. Default: 20. Capped at service level.</param>
public record PagedRequest(int Page = 1, int PageSize = 20);

/// <summary>
/// Standard paginated result wrapper returned by all list endpoints.
///
/// <para><b>Why it exists:</b>
/// Provides the client with everything needed to render pagination controls:
/// total item count, current page, and page size. The client can calculate
/// total pages as <c>Math.Ceiling(Total / PageSize)</c>.</para>
/// </summary>
/// <typeparam name="T">The type of items in the result list (typically a SummaryDto).</typeparam>
/// <param name="Items">The items for the current page.</param>
/// <param name="Total">The total number of items across all pages (for pagination UI).</param>
/// <param name="Page">The current page number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
