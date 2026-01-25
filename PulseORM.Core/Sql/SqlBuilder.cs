using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

internal static class SqlBuilder
{
    internal static (string PageSql, string CountSql, Dictionary<string, object?> Parameters)
        BuildRootKeyPage<TRoot>(
            ISqlDialect dialect,
            EntityMap rootMap,
            Expression<Func<TRoot, bool>>? where,
            Expression<Func<TRoot, object>>? orderBy,
            bool desc,
            int page,
            int pageSize)
        where TRoot : new()
    {
        var keyCol = rootMap.Key?.ColumnName;
        if (string.IsNullOrWhiteSpace(keyCol))
            throw new InvalidOperationException(
                $"Pagination requires key mapping for {typeof(TRoot).Name}.");

        var parameters = new Dictionary<string, object?>();

        var baseSql = $"FROM {rootMap.TableName} r";
        var whereSql = "";

        if (where is not null)
        {
            var w = WhereBuilder.Build(where, rootMap, dialect, tableAlias: "r");
            if (!string.IsNullOrWhiteSpace(w.Sql))
                whereSql = w.Sql;

            foreach (var kv in w.Parameters)
                parameters[kv.Key] = kv.Value;
        }

        var whereClause = string.IsNullOrWhiteSpace(whereSql)
            ? "WHERE 1=1"
            : $"WHERE {whereSql}";

        var orderSql = OrderByBuilder.Build(orderBy, rootMap, desc);

        var countSql = $"SELECT COUNT(*) {baseSql} {whereClause}";

        var skip = (page - 1) * pageSize;
        var baseSelectSql = $"SELECT r.{keyCol} {baseSql} {whereClause}";
        var pageSql = dialect.ApplyPagination(
            baseSelectSql,
            skip,
            pageSize,
            orderSql);

        return (pageSql, countSql, parameters);
    }

    internal static (string Sql, Dictionary<string, object?> Parameters) BuildRootOnly<TRoot>(
        ISqlDialect dialect,
        EntityMap rootMap,
        Expression<Func<TRoot, bool>>? where,
        Expression<Func<TRoot, object>>? orderBy,
        bool desc)
        where TRoot : new()
    {
        var parameters = new Dictionary<string, object?>();

        var select = BuildRootSelectList(rootMap, "r");
        var sql = $"SELECT {select} FROM {rootMap.TableName} r";


        var whereSql = "";
        if (where is not null)
        {
            var w = WhereBuilder.Build(where, rootMap, dialect, tableAlias: "r");
            if (!string.IsNullOrWhiteSpace(w.Sql))
                whereSql = w.Sql;

            foreach (var kv in w.Parameters)
                parameters[kv.Key] = kv.Value;
        }

        sql += string.IsNullOrWhiteSpace(whereSql) ? " WHERE 1=1" : $" WHERE {whereSql}";

        var orderSql = OrderByBuilder.Build(orderBy, rootMap, desc);
        sql += " " + orderSql;

        return (sql, parameters);
    }

    internal static (string Sql, Dictionary<string, object?> Parameters) BuildSelectRootByKeys<TRoot>(
        ISqlDialect dialect,
        EntityMap rootMap,
        IReadOnlyCollection<object> keys,
        Expression<Func<TRoot, object>>? orderBy,
        bool desc)
        where TRoot : new()
    {
        if (keys.Count == 0)
        {
            var emptySelect = BuildRootSelectList(rootMap, "r");
            return ($"SELECT {emptySelect} FROM {rootMap.TableName} r WHERE 1=0", new Dictionary<string, object?>());
        }
        
        var keyCol = rootMap.Key?.ColumnName;
        if (string.IsNullOrWhiteSpace(keyCol))
            throw new InvalidOperationException(
                $"Selecting by keys requires key mapping for {typeof(TRoot).Name}.");

        var parameters = new Dictionary<string, object?>();
        var inClause = AppendInClause(dialect, $"r.{keyCol}", keys.ToList(), parameters);

        var select = BuildRootSelectList(rootMap, "r");
        var sql = $"SELECT {select} FROM {rootMap.TableName} r WHERE {inClause}";


        var orderSql = OrderByBuilder.Build(orderBy, rootMap, desc);
        sql += " " + orderSql;

        return (sql, parameters);
    }

    internal static (string Sql, Dictionary<string, object?> Parameters) BuildJoined<TRoot>(
        ISqlDialect dialect,
        EntityMap rootMap,
        List<(EntityMap Map, IJoinSpec Spec)> joins,
        Expression<Func<TRoot, bool>>? where,
        Expression<Func<TRoot, object>>? orderBy,
        bool desc) where TRoot : new()
    {
        var parameters = new Dictionary<string, object?>();

        var select = BuildSelectList(rootMap, "r", "r__", joins);
        var sql = $"SELECT {select} FROM {rootMap.TableName} r";

        foreach (var (jm, spec) in joins)
        {
            var joinKw = spec.JoinKind == JoinType.Left ? "LEFT JOIN" : "INNER JOIN";
            var rootCol = rootMap.PropertyByName[spec.RootKeyMember.Name].ColumnName;
            var joinCol = jm.PropertyByName[spec.JoinKeyMember.Name].ColumnName;

            sql += $" {joinKw} {jm.TableName} {spec.Alias} ON r.{rootCol} = {spec.Alias}.{joinCol}";
        }

        var whereSql = "";
        if (where is not null)
        {
            var w = WhereBuilder.Build(where, rootMap, dialect, tableAlias: "r");
            if (!string.IsNullOrWhiteSpace(w.Sql))
                whereSql = w.Sql;
            foreach (var kv in w.Parameters) parameters[kv.Key] = kv.Value;
        }

        sql += string.IsNullOrWhiteSpace(whereSql) ? " WHERE 1=1" : $" WHERE {whereSql}";

        var orderSql = OrderByBuilder.Build(orderBy, rootMap, desc);
        sql += " " + orderSql;

        return (sql, parameters);
    }

    private static string BuildSelectList(
        EntityMap rootMap,
        string rootAlias,
        string rootPrefix,
        List<(EntityMap Map, IJoinSpec Spec)> joins)
    {
        var parts = new List<string>();

        foreach (var p in rootMap.Properties)
            parts.Add($"{rootAlias}.{p.ColumnName} AS {rootPrefix}{p.ColumnName}");

        foreach (var (jm, spec) in joins)
        {
            foreach (var p in jm.Properties)
                parts.Add($"{spec.Alias}.{p.ColumnName} AS {spec.Prefix}{p.ColumnName}");
        }

        return string.Join(", ", parts);
    }

    internal static string AppendInClause(
        ISqlDialect dialect,
        string leftExpr,
        List<object> keys,
        Dictionary<string, object?> parameters)
    {
        var names = new List<string>();
        var start = parameters.Count;

        for (var i = 0; i < keys.Count; i++)
        {
            var k = $"p{start + i}";
            parameters[k] = keys[i];
            names.Add(dialect.Param(k));
        }

        return $"{leftExpr} IN ({string.Join(", ", names)})";
    }
    
    internal static string BuildRootSelectList(EntityMap map, string alias)
    {
        return string.Join(", ", map.Properties.Select(p => $"{alias}.{p.ColumnName} AS {p.ColumnName}"));
    }

    
}
