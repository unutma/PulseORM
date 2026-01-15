using System.Linq.Expressions;
using PulseORM.Core;

internal sealed class RootPlan
{
    public string Sql { get; init; } = "";
    public string? CountSql { get; init; }
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public EntityMap RootMap { get; init; } = null!;
    public string RootAlias { get; init; } = "r";

    public static RootPlan Build<TRoot>(
        ISqlDialect dialect,
        Expression<Func<TRoot, bool>>? where,
        Expression<Func<TRoot, object>>? orderBy,
        bool desc,
        int? page,
        int? pageSize)
        where TRoot : new()
    {
        var rootMap = ModelMapper.GetMap<TRoot>();

        if (page.HasValue && pageSize.HasValue)
        {
            if (rootMap.Key is null)
                throw new InvalidOperationException($"Root type {typeof(TRoot).Name} must have a key mapped for pagination.");

            var phase1 = SqlBuilder.BuildRootKeyPage(dialect, rootMap, where, orderBy, desc, page.Value, pageSize.Value);

            return new RootPlan
            {
                Sql = phase1.PageSql,
                CountSql = phase1.CountSql,
                Parameters = phase1.Parameters,
                RootMap = rootMap,
                RootAlias = "r"
            };
        }

        var rootOnly = SqlBuilder.BuildRootOnly(dialect, rootMap, where, orderBy, desc);

        return new RootPlan
        {
            Sql = rootOnly.Sql,
            CountSql = null,
            Parameters = rootOnly.Parameters,
            RootMap = rootMap,
            RootAlias = "r"
        };
    }
}