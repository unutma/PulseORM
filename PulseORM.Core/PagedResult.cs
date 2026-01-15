namespace PulseORM.Core;

public sealed record PagedResult<T>(List<T> Items, long TotalCount, int Page, int PageSize);
