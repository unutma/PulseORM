using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

public sealed class PulseQueryJoin<TRoot> where TRoot : new()
{
    private readonly PulseLiteDb _db;
    private HashSet<string>? _rootSelectProps;
    private readonly Dictionary<Type, HashSet<string>> _joinSelectProps = new();
    private LambdaExpression? _projector;

    private Expression<Func<TRoot, bool>>? _where;
    private Expression<Func<TRoot, object>>? _orderBy;
    private bool _desc;
    private int? _page;
    private int? _pageSize;

    private readonly List<IJoinSpec> _joins = new();

    internal PulseQueryJoin(PulseLiteDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public PulseQueryJoin<TRoot> FilterSql(Expression<Func<TRoot, bool>> predicate)
    {
        _where = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    public PulseQueryJoin<TRoot> SortBy(Expression<Func<TRoot, object>> orderBy, bool descending = false)
    {
        _orderBy = orderBy ?? throw new ArgumentNullException(nameof(orderBy));
        _desc = descending;
        return this;
    }

    public PulseQueryJoin<TRoot> Pagination(int page, int pageSize)
    {
        if (page <= 0) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        _page = page;
        _pageSize = pageSize;
        return this;
    }
    public PulseQueryJoin<TRoot> ProjectTo<TDto>(Expression<Func<TRoot, TDto>> selector)
    {
        _projector = selector ?? throw new ArgumentNullException(nameof(selector));
        return this;
    }


    public PulseQueryJoin<TRoot> IncludeOne<TJoin>(
        Expression<Func<TRoot, TJoin?>> nav,
        Expression<Func<TRoot, object>> rootKey,
        Expression<Func<TJoin, object>> joinKey,
        JoinType joinType)
        where TJoin : new()
    {
        _joins.Add(new JoinSpecOne<TRoot, TJoin>(nav, rootKey, joinKey, joinType));
        return this;
    }

    public PulseQueryJoin<TRoot> SelectRootColumns(params Expression<Func<TRoot, object>>[] cols)
    {
        _rootSelectProps ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cols)
            _rootSelectProps.Add(ExpressionHelper.ExtractMember(c.Body).Name);
        return this;
    }

    public PulseQueryJoin<TRoot> SelectJoinColumns<TJoin>(params Expression<Func<TJoin, object>>[] cols)
        where TJoin : new()
    {
        var t = typeof(TJoin);
        if (!_joinSelectProps.TryGetValue(t, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _joinSelectProps[t] = set;
        }

        foreach (var c in cols)
            set.Add(ExpressionHelper.ExtractMember(c.Body).Name);

        return this;
    }

    public async Task<(List<TRoot> Items, long TotalCount)> ToListAsync()
    {
        if (_joins.Count == 0)
            throw new InvalidOperationException("PulseQueryJoin requires at least one IncludeOne/IncludeMany.");

        var rootResult = await ExecuteRootAsync().ConfigureAwait(false);
        await ApplySplitIncludesAsync(rootResult.Items).ConfigureAwait(false);
        return rootResult;
    }

    public async Task<(List<TDto> Items, long TotalCount)> ToListAsync<TDto>()
    {
        if (_joins.Count == 0)
            throw new InvalidOperationException("PulseQueryJoin requires at least one IncludeOne/IncludeMany.");

        if (_projector is null)
            throw new InvalidOperationException("ProjectTo<TDto>() must be set before calling ToListAsync<TDto>().");

        if (_projector is not Expression<Func<TRoot, TDto>> typed)
            throw new InvalidOperationException($"ProjectTo selector type mismatch. Expected {typeof(TDto).Name}.");

        var rootResult = await ExecuteRootAsync().ConfigureAwait(false);
        await ApplySplitIncludesAsync(rootResult.Items).ConfigureAwait(false);

        var fn = typed.Compile();
        var projected = new List<TDto>(rootResult.Items.Count);

        foreach (var r in rootResult.Items)
            projected.Add(fn(r));

        return (projected, rootResult.TotalCount);
    }

    private async Task<(List<TRoot> Items, long TotalCount)> ExecuteRootAsync()
    {
        var rootMap = ModelMapper.GetMap<TRoot>();
        var required = BuildRequiredRootProps(rootMap);
        var rootProps = MergeProps(_rootSelectProps, required);

        if (!_page.HasValue || !_pageSize.HasValue)
        {
            var rootOnly = SqlBuilder.BuildRootOnly<TRoot>(
                _db._dialect,
                rootMap,
                _where,
                _orderBy,
                _desc);

            var alias = TryExtractAlias(rootOnly.Sql) ?? "t";
            var selectList = BuildSelectList(rootMap, alias, rootProps);
            var sql = ReplaceSelectList(rootOnly.Sql, selectList);

            var items = await _db.QueryAsync<TRoot>(sql, rootOnly.Parameters).ConfigureAwait(false);
            return (items, items.Count);
        }

        if (rootMap.Key is null)
            throw new InvalidOperationException(
                $"Root type {typeof(TRoot).Name} must have a key mapped for pagination.");

        var pageSpec = SqlBuilder.BuildRootKeyPage(
            _db._dialect,
            rootMap,
            _where,
            _orderBy,
            _desc,
            _page.Value,
            _pageSize.Value);

        var total = await _db.QueryCountSqlAsync(pageSpec.CountSql, pageSpec.Parameters).ConfigureAwait(false);
        if (total == 0)
            return (new List<TRoot>(), 0);

        var keys = await _db.QueryScalarListAsync<object>(pageSpec.PageSql, pageSpec.Parameters).ConfigureAwait(false);
        if (keys.Count == 0)
            return (new List<TRoot>(), total);

        var selectSpec = SqlBuilder.BuildSelectRootByKeys<TRoot>(
            _db._dialect,
            rootMap,
            keys,
            _orderBy,
            _desc);

        var alias2 = TryExtractAlias(selectSpec.Sql) ?? "t";
        var selectList2 = BuildSelectList(rootMap, alias2, rootProps);
        var sql2 = ReplaceSelectList(selectSpec.Sql, selectList2);

        var result = await _db.QueryAsync<TRoot>(sql2, selectSpec.Parameters).ConfigureAwait(false);
        return (result, total);
    }

    private HashSet<string> BuildRequiredRootProps(EntityMap rootMap)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_page.HasValue && _pageSize.HasValue && rootMap.Key is not null)
            set.Add(rootMap.Key.PropertyInfo.Name);

        foreach (var j in _joins)
            set.Add(j.RootKeyMember.Name);

        if (_orderBy is not null)
            set.Add(ExpressionHelper.ExtractMember(_orderBy.Body).Name);

        return set;
    }

    private static HashSet<string>? MergeProps(HashSet<string>? explicitProps, HashSet<string> required)
    {
        if (explicitProps is null || explicitProps.Count == 0)
            return null;

        var merged = new HashSet<string>(explicitProps, StringComparer.OrdinalIgnoreCase);
        foreach (var r in required)
            merged.Add(r);

        return merged;
    }

    private static string BuildSelectList(EntityMap map, string alias, HashSet<string>? props)
    {
        if (props is null || props.Count == 0)
            return SqlBuilder.BuildRootSelectList(map, alias);

        var parts = new List<string>(props.Count);

        foreach (var p in props)
        {
            if (!map.PropertyByName.TryGetValue(p, out var pm))
                throw new InvalidOperationException($"Property '{p}' is not mapped for table '{map.TableName}'.");

            parts.Add($"{alias}.{pm.ColumnName} AS {pm.PropertyInfo.Name}");
        }

        return string.Join(", ", parts);
    }

    private static string ReplaceSelectList(string sql, string newSelectList)
    {
        var sel = IndexOfIgnoreCase(sql, "SELECT");
        if (sel < 0) return sql;

        var from = IndexOfIgnoreCase(sql, " FROM ", sel);
        if (from < 0) return sql;

        var before = sql.Substring(0, sel + "SELECT".Length);
        var after = sql.Substring(from);
        return $"{before} {newSelectList}{after}";
    }

    private static int IndexOfIgnoreCase(string s, string value, int startIndex = 0)
    {
        return s.IndexOf(value, startIndex, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractAlias(string sql)
    {
        var from = IndexOfIgnoreCase(sql, " FROM ");
        if (from < 0) return null;

        var tail = sql.Substring(from + " FROM ".Length).TrimStart();
        if (tail.Length == 0) return null;

        var tokens = TokenizeSqlTail(tail);
        if (tokens.Count < 2) return null;

        var alias = tokens[1];

        if (IsSqlKeyword(alias))
            return null;

        return alias;
    }

    private static List<string> TokenizeSqlTail(string s)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inBrackets = false;
        bool inQuotes = false;
        char quoteChar = '\0';

        for (int i = 0; i < s.Length; i++)
        {
            var ch = s[i];

            if (inQuotes)
            {
                sb.Append(ch);
                if (ch == quoteChar)
                    inQuotes = false;
                continue;
            }

            if (ch == '\'' || ch == '"')
            {
                inQuotes = true;
                quoteChar = ch;
                sb.Append(ch);
                continue;
            }

            if (ch == '[')
            {
                inBrackets = true;
                sb.Append(ch);
                continue;
            }

            if (ch == ']')
            {
                inBrackets = false;
                sb.Append(ch);
                continue;
            }

            if (!inBrackets && char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
                if (tokens.Count >= 2)
                    break;
                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0 && tokens.Count < 2)
            tokens.Add(sb.ToString());

        return tokens;
    }

    private static bool IsSqlKeyword(string token)
    {
        var t = token.Trim().ToUpperInvariant();
        return t is "WHERE" or "JOIN" or "LEFT" or "RIGHT" or "INNER" or "FULL" or "CROSS" or "ON"
            or "ORDER" or "GROUP" or "HAVING" or "LIMIT" or "OFFSET" or "FETCH" or "UNION";
    }

    private async Task ApplySplitIncludesAsync(List<TRoot> roots)
    {
        if (roots.Count == 0)
            return;

        foreach (var j in _joins.Where(x => !x.IsMany))
            await ApplyIncludeOneAsync(roots, j).ConfigureAwait(false);

        foreach (var j in _joins.Where(x => x.IsMany))
            await ApplyIncludeManyAsync(roots, j).ConfigureAwait(false);
    }

    private async Task ApplyIncludeOneAsync(List<TRoot> roots, IJoinSpec join)
    {
        var rootKeys = new List<object>();

        foreach (var r in roots)
        {
            var rk = GetMemberValue(r!, join.RootKeyMember);
            if (rk is not null) rootKeys.Add(rk);
        }

        rootKeys = rootKeys.Distinct().ToList();
        if (rootKeys.Count == 0)
            return;

        var joinMap = ModelMapper.GetMap(join.JoinTypeClr);
        var joinKeyCol = joinMap.PropertyByName[join.JoinKeyMember.Name].ColumnName;

        var joined = await QueryByAnyAsync(join, joinMap, joinMap.TableName, joinKeyCol, rootKeys).ConfigureAwait(false);

        var joinedByKey = new Dictionary<object, object>();
        foreach (var j in joined)
        {
            var jk = GetMemberValue(j, join.JoinKeyMember);
            if (jk is null) continue;
            if (!joinedByKey.ContainsKey(jk))
                joinedByKey.Add(jk, j);
        }

        if (join.JoinKind == JoinType.Inner)
        {
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                var rk = GetMemberValue(roots[i]!, join.RootKeyMember);
                if (rk is null || !joinedByKey.ContainsKey(rk))
                    roots.RemoveAt(i);
            }
        }

        foreach (var r in roots)
        {
            var rk = GetMemberValue(r!, join.RootKeyMember);
            joinedByKey.TryGetValue(rk!, out var j);
            join.ApplyOne(r!, j);
        }
    }

    private static object? GetMemberValue(object target, MemberInfo member)
    {
        return member switch
        {
            PropertyInfo pi => pi.GetValue(target),
            FieldInfo fi => fi.GetValue(target),
            _ => throw new InvalidOperationException($"Unsupported member type: {member.MemberType}")
        };
    }

    private async Task ApplyIncludeManyAsync(List<TRoot> roots, IJoinSpec join)
    {
        var rootKeyProp = (PropertyInfo)join.RootKeyMember;
        var rootKeys = roots
            .Select(r => rootKeyProp.GetValue(r))
            .Where(v => v is not null)
            .Distinct()
            .ToArray();

        if (rootKeys.Length == 0)
            return;

        var joinMap = ModelMapper.GetMap(join.JoinTypeClr);
        var joinFkProp = (PropertyInfo)join.JoinKeyMember;
        var joinFkColumn = GetColumnName(joinMap, joinFkProp);

        var entities = await QueryByAnyAsync(join, joinMap, joinMap.TableName, joinFkColumn, rootKeys).ConfigureAwait(false);

        var groups = new Dictionary<object, List<object>>();
        foreach (var e in entities)
        {
            var fk = joinFkProp.GetValue(e);
            if (fk is null)
                continue;

            if (!groups.TryGetValue(fk, out var list))
            {
                list = new List<object>();
                groups[fk] = list;
            }
            list.Add(e);
        }

        foreach (var root in roots)
        {
            var key = rootKeyProp.GetValue(root);
            if (key is null || !groups.TryGetValue(key, out var list))
                continue;

            foreach (var child in list)
                join.ApplyMany(root, child);
        }
    }

    private async Task<List<object>> QueryByAnyAsync(
        IJoinSpec join,
        EntityMap joinMap,
        string tableName,
        string keyColumn,
        object ids)
    {
        var idList = NormalizeIds(ids);
        if (idList.Count == 0)
            return new List<object>();

        var props = ResolveJoinProps(join, joinMap);
        var select = BuildSelectList(joinMap, "t", props);

        var parameters = new Dictionary<string, object?>();
        var inClause = SqlBuilder.AppendInClause(_db._dialect, $"t.{keyColumn}", idList, parameters);
        var sql = $"SELECT {select} FROM {tableName} t WHERE {inClause}";

        return await QueryAsyncByType(join.JoinTypeClr, sql, parameters).ConfigureAwait(false);
    }

    private HashSet<string>? ResolveJoinProps(IJoinSpec join, EntityMap joinMap)
    {
        if (!_joinSelectProps.TryGetValue(join.JoinTypeClr, out var explicitProps) || explicitProps.Count == 0)
            return null;

        var merged = new HashSet<string>(explicitProps, StringComparer.OrdinalIgnoreCase);

        merged.Add(join.JoinKeyMember.Name);

        if (joinMap.Key is not null)
            merged.Add(joinMap.Key.PropertyInfo.Name);

        return merged;
    }

    private static List<object> NormalizeIds(object ids)
    {
        if (ids is null)
            return new List<object>();

        if (ids is IEnumerable enumerable && ids is not string)
        {
            var list = new List<object>();
            foreach (var item in enumerable)
            {
                if (item is not null)
                    list.Add(item);
            }
            return list;
        }

        throw new ArgumentException($"ids must be an IEnumerable, got {ids.GetType().FullName}");
    }

    private async Task<List<object>> QueryAsyncByType(Type clrType, string sql, Dictionary<string, object?> parameters)
    {
        var dbType = _db.GetType();

        var mi = dbType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == "QueryAsync")
            .Where(m => m.IsGenericMethodDefinition)
            .Where(m =>
            {
                var ps = m.GetParameters();
                if (ps.Length != 2) return false;
                if (ps[0].ParameterType != typeof(string)) return false;
                return ps[1].ParameterType.IsAssignableFrom(typeof(Dictionary<string, object?>))
                       || typeof(Dictionary<string, object?>).IsAssignableFrom(ps[1].ParameterType);
            })
            .Where(m =>
            {
                var rt = m.ReturnType;
                if (!rt.IsGenericType) return false;
                if (rt.GetGenericTypeDefinition() != typeof(Task<>)) return false;

                var inner = rt.GetGenericArguments()[0];
                if (!inner.IsGenericType) return false;
                if (inner.GetGenericTypeDefinition() != typeof(List<>)) return false;

                return true;
            })
            .SingleOrDefault();

        if (mi is null)
        {
            var candidates = dbType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "QueryAsync" && m.IsGenericMethodDefinition)
                .Select(m =>
                {
                    var ps = m.GetParameters();
                    var p1 = ps.Length > 0 ? ps[0].ParameterType.FullName : "";
                    var p2 = ps.Length > 1 ? ps[1].ParameterType.FullName : "";
                    return $"{m.ReturnType.FullName} {m.Name}<{m.GetGenericArguments().Length}>({p1}, {p2})";
                });

            throw new InvalidOperationException(
                "QueryAsync<T>(string, dict) returning Task<List<T>> not found. Candidates:\n" + string.Join("\n", candidates));
        }

        var gmi = mi.MakeGenericMethod(clrType);

        object arg1 = parameters;
        var taskObj = gmi.Invoke(_db, new object[] { sql, arg1 })!;

        try
        {
            await ((Task)taskObj).ConfigureAwait(false);
        }
        catch
        {
            Console.WriteLine(sql);
            foreach (var kv in parameters)
                Console.WriteLine($"{kv.Key} = {kv.Value} ({kv.Value?.GetType().FullName ?? "null"})");
            throw;
        }

        var resultProp = taskObj.GetType().GetProperty("Result");
        if (resultProp is null)
            throw new InvalidOperationException($"QueryAsync returned non-generic Task. ReturnType={mi.ReturnType.FullName}");

        var result = (IEnumerable)resultProp.GetValue(taskObj)!;
        return result.Cast<object>().ToList();
    }

    private static string GetColumnName(EntityMap map, PropertyInfo prop)
    {
        var p = map.Properties.FirstOrDefault(x => x.PropertyInfo == prop);
        if (p is null)
            throw new InvalidOperationException($"No column mapping found for {map.Type.Name}.{prop.Name}");
        return p.ColumnName;
    }
}
