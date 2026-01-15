using System.Linq.Expressions;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

public sealed class SqlQuery<T> where T : new()
{
    private readonly PulseLiteDb _db;
    private readonly EntityMap _map;
    private readonly ISqlDialect _dialect;

    private readonly string _baseSql;
    private readonly Dictionary<string, object?> _parameters;

    private Expression<Func<T, bool>>? _where;

    internal SqlQuery(
        PulseLiteDb db,
        string baseSql,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        _db = db;
        _map = ModelMapper.GetMap<T>();
        _dialect = db._dialect;

        _baseSql = baseSql.Trim();
        _parameters = parameters is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(parameters);
    }

    public SqlQuery<T> FilterSql(Expression<Func<T, bool>> predicate)
    {
        _where = _where is null ? predicate : CombineAnd(_where, predicate);
        return this;
    }

    public Task<List<T>> ToListAsync()
    {
        var sql = _baseSql;

        if (_where is not null)
        {
            var where = WhereBuilder.Build<T>(
                _where,
                _map,
                _dialect
            );

            if (!string.IsNullOrWhiteSpace(where.Sql))
            {
                sql += " WHERE " + where.Sql;

                foreach (var kv in where.Parameters)
                    _parameters[kv.Key] = kv.Value;
            }
        }

        return _db.QueryAsync<T>(sql, _parameters);
    }

    public async Task<T?> SingleOrDefaultSqlAsync()
    {
        var list = await ToListAsync().ConfigureAwait(false);
        if (list.Count == 0) return default;
        if (list.Count > 1)
            throw new InvalidOperationException("Sequence contains more than one element.");
        return list[0];
    }

    private static Expression<Func<T, bool>> CombineAnd(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var p = Expression.Parameter(typeof(T), "x");
        var l = new ReplaceParamVisitor(left.Parameters[0], p).Visit(left.Body)!;
        var r = new ReplaceParamVisitor(right.Parameters[0], p).Visit(right.Body)!;
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(l, r), p);
    }

    private sealed class ReplaceParamVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ReplaceParamVisitor(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }
}
