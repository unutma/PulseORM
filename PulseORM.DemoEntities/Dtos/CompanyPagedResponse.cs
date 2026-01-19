using PulseORM.DemoEntities.Tables;

namespace PulseORM.DemoEntities.Dtos;

public sealed class CompanyPagedResponse
{
    public IEnumerable<Company> Companies { get; init; } = [];
    public long TotalCount { get; init; }
}
