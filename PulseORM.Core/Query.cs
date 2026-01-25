using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

public sealed class Query<T> where T : new()
{
    private readonly PulseLiteDb _db;
    private readonly EntityMap _map;
    private readonly ISqlDialect _dialect;

    private Expression<Func<T, bool>>? _where;

    internal Query(PulseLiteDb db)
    {
        _db = db;
        _map = ModelMapper.GetMap<T>();
        _dialect = db._dialect;
    }

    public Query<T> FilterSql(Expression<Func<T, bool>> predicate)
    {
        _where = _where is null ? predicate : CombineAnd(_where, predicate);
        return this;
    }

    public Task<List<T>> ToListAsync()
{
    var select = SqlBuilder.BuildRootSelectList(_map, "t");
    var sql = $"SELECT {select} FROM {_map.TableName} t";

    var where = WhereBuilder.Build<T>(_where, _map, _dialect, "t", 0);
    if (!string.IsNullOrWhiteSpace(where.Sql))
        sql += " WHERE " + where.Sql;

    return _db.QueryAsync<T>(sql, where.Parameters);
}
    public Task<List<TDto>> ToListSelectAsync<TDto>()
        where TDto : new()
    {
        var dtoProps = typeof(TDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToArray();

        return ToListSelectAsync<TDto>(dtoProps);
    }

    
    private Task<List<TDto>> ToListSelectAsync<TDto>(params string[] columns)
        where TDto : new()
    {
        var selectSql = BuildSelectSqlForDto<TDto>(columns);
        var sql = $"SELECT {selectSql} FROM {_map.TableName} t";

        var where = WhereBuilder.Build<T>(_where, _map, _dialect, "t", 0);
        if (!string.IsNullOrWhiteSpace(where.Sql))
            sql += " WHERE " + where.Sql;

        return _db.QueryAsync<TDto>(sql, where.Parameters);
    }
    
    private string BuildSelectSqlForDto<TDto>(string[] columns)
    {
        if (columns is null || columns.Length == 0)
            throw new InvalidOperationException("At least one column must be specified for DTO projection.");

        var dtoProps = typeof(TDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var parts = new List<string>(columns.Length);

        foreach (var propName in columns)
        {
            if (!_map.PropertyByName.TryGetValue(propName, out var mp))
                throw new InvalidOperationException($"Property '{propName}' is not mapped for table '{_map.TableName}'.");

            if (!dtoProps.ContainsKey(propName))
                throw new InvalidOperationException($"DTO '{typeof(TDto).Name}' does not have a property named '{propName}'.");

            parts.Add($"t.{mp.ColumnName} AS {propName}");
        }

        return string.Join(", ", parts);
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
