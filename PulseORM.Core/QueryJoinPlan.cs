using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

public enum JoinType { Inner, Left }

internal interface IJoinSpec
{
    bool IsMany { get; }
    Type JoinTypeClr { get; }
    string Alias { get; set; }
    string Prefix { get; set; }
    JoinType JoinKind { get; }
    MemberInfo RootKeyMember { get; }
    MemberInfo JoinKeyMember { get; }
    void ApplyOne(object root, object? joined);
    void ApplyMany(object root, object? joined);
}

internal sealed class JoinSpecOne<TRoot, TJoin> : IJoinSpec
    where TRoot : new()
    where TJoin : new()
{
    private readonly Action<TRoot, TJoin?> _setNav;
    public bool IsMany => false;
    public Type JoinTypeClr => typeof(TJoin);
    public string Alias { get; set; } = "";
    public string Prefix { get; set; } = "";
    public JoinType JoinKind { get; }
    public MemberInfo RootKeyMember { get; }
    public MemberInfo JoinKeyMember { get; }

    public JoinSpecOne(
        Expression<Func<TRoot, TJoin?>> nav,
        Expression<Func<TRoot, object>> rootKey,
        Expression<Func<TJoin, object>> joinKey,
        JoinType joinKind)
    {
        var navMember = (PropertyInfo)ExpressionHelper.ExtractMember(nav.Body);

        _setNav = (root, joined) =>
        {
            navMember.SetValue(root, joined);
        };

        RootKeyMember = ExpressionHelper.ExtractMember(rootKey.Body);
        JoinKeyMember = ExpressionHelper.ExtractMember(joinKey.Body);
        JoinKind = joinKind;

    }

    public void ApplyOne(object root, object? joined) => _setNav((TRoot)root, (TJoin?)joined);
    public void ApplyMany(object root, object? joined) { }

    private static Action<TRoot, TJoin?> CompileSetter(Expression<Func<TRoot, TJoin?>> nav)
    {
        if (nav.Body is not MemberExpression me || me.Member is not PropertyInfo pi)
            throw new InvalidOperationException("nav must be a property access.");

        var target = Expression.Parameter(typeof(TRoot), "t");
        var value = Expression.Parameter(typeof(TJoin), "v");

        var assign = Expression.Assign(Expression.Property(target, pi), Expression.Convert(value, pi.PropertyType));
        var lambda = Expression.Lambda<Action<TRoot, TJoin?>>(assign, target, Expression.Parameter(typeof(TJoin), "v2"));
        return lambda.Compile();
    }

    private static MemberInfo ExtractMember(LambdaExpression expr)
    {
        var body = expr.Body is UnaryExpression u ? u.Operand : expr.Body;
        if (body is MemberExpression me) return me.Member;
        throw new InvalidOperationException("Key expression must be a member access.");
    }
}

internal sealed class JoinSpecMany<TRoot, TJoin> : IJoinSpec
    where TRoot : new()
    where TJoin : new()
{
    private readonly Func<TRoot, IList<TJoin>> _getList;

    public bool IsMany => true;
    public Type JoinTypeClr => typeof(TJoin);
    public string Alias { get; set; } = "";
    public string Prefix { get; set; } = "";
    public JoinType JoinKind { get; }
    public MemberInfo RootKeyMember { get; }
    public MemberInfo JoinKeyMember { get; }

    public JoinSpecMany(
        Expression<Func<TRoot, IList<TJoin>>> nav,
        Expression<Func<TRoot, object>> rootKey,
        Expression<Func<TJoin, object>> joinFk,
        JoinType joinKind)
    {
        _getList = nav.Compile();
        RootKeyMember = ExtractMember(rootKey);
        JoinKeyMember = ExtractMember(joinFk);
        JoinKind = joinKind;
    }

    public void ApplyOne(object root, object? joined) { }

    public void ApplyMany(object root, object? joined)
    {
        if (joined is null) return;
        _getList((TRoot)root).Add((TJoin)joined);
    }

    private static MemberInfo ExtractMember(LambdaExpression expr)
    {
        var body = expr.Body is UnaryExpression u ? u.Operand : expr.Body;
        if (body is MemberExpression me) return me.Member;
        throw new InvalidOperationException("Key expression must be a member access.");
    }
}

internal sealed class QueryPlan
{
    public string Sql { get; init; } = "";
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public EntityMap RootMap { get; init; } = null!;
    public string RootAlias { get; init; } = "r";
    public string RootPrefix { get; init; } = "r__";
    public List<(EntityMap Map, IJoinSpec Spec)> Joins { get; init; } = new();

    public static QueryPlan Build<TRoot>(
        ISqlDialect dialect,
        List<IJoinSpec> joins,
        Expression<Func<TRoot, bool>>? where,
        Expression<Func<TRoot, object>>? orderBy,
        bool desc) where TRoot : new()
    {
        var rootMap = ModelMapper.GetMap<TRoot>();
        var planJoins = new List<(EntityMap, IJoinSpec)>();

        for (var i = 0; i < joins.Count; i++)
        {
            joins[i].Alias = $"j{i}";
            joins[i].Prefix = $"j{i}__";
            planJoins.Add((ModelMapper.GetMap(joins[i].JoinTypeClr), joins[i]));
        }

        var (sql, parameters) = SqlBuilder.BuildJoined(dialect, rootMap, planJoins, where, orderBy, desc);

        return new QueryPlan
        {
            Sql = sql,
            Parameters = parameters,
            RootMap = rootMap,
            Joins = planJoins
        };
    }

    public static QueryPlan BuildByKeys<TRoot>(
        ISqlDialect dialect,
        List<IJoinSpec> joins,
        Expression<Func<TRoot, bool>>? where,
        Expression<Func<TRoot, object>>? orderBy,
        bool desc,
        List<object> keys) where TRoot : new()
    {
        var plan = Build(dialect, joins, where, orderBy, desc);

        var keyCol = plan.RootMap.Key?.ColumnName;
        if (string.IsNullOrWhiteSpace(keyCol))
            throw new InvalidOperationException($"Root key not mapped for {typeof(TRoot).Name}.");

        var inClause = SqlBuilder.AppendInClause(dialect, $"{plan.RootAlias}.{keyCol}", keys, plan.Parameters);

        return new QueryPlan
        {
            Sql = $"{plan.Sql} AND {inClause}",
            Parameters = plan.Parameters,
            RootMap = plan.RootMap,
            RootAlias = plan.RootAlias,
            RootPrefix = plan.RootPrefix,
            Joins = plan.Joins
        };
    }

}
