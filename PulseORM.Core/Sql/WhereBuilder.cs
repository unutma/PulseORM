using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using PulseORM.Core.Helper;

namespace PulseORM.Core.Sql;

internal sealed class WhereBuilder
{
    public string Sql { get; }
    public Dictionary<string, object?> Parameters { get; }

    private WhereBuilder(string sql, Dictionary<string, object?> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    internal static WhereBuilder Build<T>(
        Expression<Func<T, bool>>? predicate,
        EntityMap map,
        ISqlDialect dialect,
        string? tableAlias = null,
        int startParamIndex = 0)
    {
        if (predicate is null)
            return new WhereBuilder(string.Empty, new Dictionary<string, object?>());

        var ctx = new Context(map, dialect, tableAlias, startParamIndex);
        var sql = VisitBool(predicate.Body, ctx);
        return new WhereBuilder(sql, ctx.Parameters);
    }

    private sealed class Context
    {
        public EntityMap Map { get; }
        public ISqlDialect Dialect { get; }
        public string? Alias { get; }
        public int NextParamIndex { get; set; }
        public Dictionary<string, object?> Parameters { get; } = new();

        public Context(EntityMap map, ISqlDialect dialect, string? alias, int startParamIndex)
        {
            Map = map;
            Dialect = dialect;
            Alias = alias;
            NextParamIndex = startParamIndex;
        }

        public string AddParam(object? value)
        {
            var name = $"p{NextParamIndex++}";
            Parameters[name] = value;
            return Dialect.Param(name);
        }
    }

    private static string VisitBool(Expression expr, Context ctx)
    {
        expr = StripConvert(expr);

        if (expr is BinaryExpression b)
        {
            if (b.NodeType == ExpressionType.AndAlso || b.NodeType == ExpressionType.OrElse)
            {
                var left = VisitBool(b.Left, ctx);
                var right = VisitBool(b.Right, ctx);
                var op = b.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
                return $"({left} {op} {right})";
            }

            return VisitComparison(b, ctx);
        }

        if (expr is UnaryExpression u && u.NodeType == ExpressionType.Not)
            return $"(NOT {VisitBool(u.Operand, ctx)})";

        if (expr is MemberExpression me && me.Type == typeof(bool))
        {
            var col = ResolveColumn(me, ctx);
            return $"({col} = {ctx.Dialect.BoolLiteral(true)})";
        }
        
        if (expr is MethodCallExpression mc)
            return VisitMethodCallBool(mc, ctx);

        if (expr is ConstantExpression ce && ce.Type == typeof(bool))
            return (bool)ce.Value! ? "(1=1)" : "(1=0)";

        throw new NotSupportedException();
    }

    private static string VisitComparison(BinaryExpression b, Context ctx)
    {
        var left = StripConvert(b.Left);
        var right = StripConvert(b.Right);

        if (IsNullConstant(left) || IsNullConstant(right))
        {
            var member = (MemberExpression)(IsNullConstant(left) ? right : left);
            var col = ResolveColumn(member, ctx);
            return b.NodeType == ExpressionType.Equal
                ? $"({col} IS NULL)"
                : $"({col} IS NOT NULL)";
        }

        if (IsBoolMember(left, out var leftBoolMember) && TryEvalBool(right, out var rightBool))
        {
            var col = ResolveColumn(leftBoolMember, ctx);
            return b.NodeType == ExpressionType.Equal
                ? $"({col} = {ctx.Dialect.BoolLiteral(rightBool)})"
                : $"({col} <> {ctx.Dialect.BoolLiteral(rightBool)})";
        }

        if (IsBoolMember(right, out var rightBoolMember) && TryEvalBool(left, out var leftBool))
        {
            var col = ResolveColumn(rightBoolMember, ctx);
            return b.NodeType == ExpressionType.Equal
                ? $"({col} = {ctx.Dialect.BoolLiteral(leftBool)})"
                : $"({col} <> {ctx.Dialect.BoolLiteral(leftBool)})";
        }

        var sqlOp = b.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException()
        };

        var l = VisitValue(left, ctx);
        var r = VisitValue(right, ctx);
        return $"({l} {sqlOp} {r})";
    }

    private static string VisitValue(Expression expr, Context ctx)
    {
        expr = StripConvert(expr);

        if (expr is MemberExpression me && me.Expression is ParameterExpression)
            return ResolveColumn(me, ctx);

        return ctx.AddParam(Evaluate(expr));
    }

    private static string ResolveColumn(MemberExpression member, Context ctx)
    {
        var inner = StripConvert(member.Expression!);
        if (inner is not ParameterExpression)
            throw new NotSupportedException();

        var propName = member.Member.Name;

        if (!ctx.Map.PropertyByName.TryGetValue(propName, out var pm))
            throw new InvalidOperationException(
                $"Property '{propName}' is not mapped for table '{ctx.Map.TableName}'.");

        var col = pm.ColumnName;
        return string.IsNullOrWhiteSpace(ctx.Alias) ? col : $"{ctx.Alias}.{col}";
    }

    private static bool IsBoolMember(Expression expr, out MemberExpression member)
    {
        expr = StripConvert(expr);
        if (expr is MemberExpression me && me.Type == typeof(bool) && me.Expression is ParameterExpression)
        {
            member = me;
            return true;
        }

        member = null!;
        return false;
    }

    private static bool TryEvalBool(Expression expr, out bool value)
    {
        expr = StripConvert(expr);

        if (expr is ConstantExpression ce && ce.Type == typeof(bool))
        {
            value = (bool)ce.Value!;
            return true;
        }

        try
        {
            var v = Evaluate(expr);
            if (v is bool b)
            {
                value = b;
                return true;
            }
        }
        catch { }

        value = default;
        return false;
    }

    private static bool IsNullConstant(Expression expr)
        => expr is ConstantExpression ce && ce.Value is null;

    private static object? Evaluate(Expression expr)
    {
        if (expr is ConstantExpression ce)
            return ce.Value;

        return Expression.Lambda(expr).Compile().DynamicInvoke();
    }

    private static Expression StripConvert(Expression expr)
    {
        while (expr is UnaryExpression u &&
               (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked))
            expr = u.Operand;

        return expr;
    }
    
    private static string VisitMethodCallBool(MethodCallExpression mc, Context ctx)
{
    if (TryBuildEqualsIgnoreCase(mc, ctx, out var sql))
        return sql;

    throw new NotSupportedException();
}

private static bool TryBuildEqualsIgnoreCase(MethodCallExpression mc, Context ctx, out string sql)
{
    sql = string.Empty;

    if (mc.Method.Name == "EqualsIgnoreCase" &&
        mc.Method.DeclaringType == typeof(PulseSql) &&
        mc.Arguments.Count == 2)
    {
        var left = StripConvert(mc.Arguments[0]);
        var right = StripConvert(mc.Arguments[1]);

        if (left is MemberExpression leftMember && leftMember.Type == typeof(string))
        {
            EnsureDirectParameterAccess(leftMember);
            var leftSql = ResolveColumn(leftMember, ctx);
            var rightSql = ctx.AddParam(Evaluate(right));
            sql = ctx.Dialect.EqualsIgnoreCase(leftSql, rightSql);
            return true;
        }

        if (right is MemberExpression rightMember && rightMember.Type == typeof(string))
        {
            EnsureDirectParameterAccess(rightMember);
            var leftSql = ResolveColumn(rightMember, ctx);
            var rightSql = ctx.AddParam(Evaluate(left));
            sql = ctx.Dialect.EqualsIgnoreCase(leftSql, rightSql);
            return true;
        }

        return false;
    }

    if (mc.Method.Name == "Equals")
    {
        if (mc.Object is not null && mc.Object.Type == typeof(string))
        {
            if (mc.Arguments.Count == 2 &&
                mc.Arguments[1].Type == typeof(StringComparison) &&
                TryEvalStringComparison(mc.Arguments[1], out var sc) &&
                sc == StringComparison.OrdinalIgnoreCase)
            {
                var left = StripConvert(mc.Object);
                var right = StripConvert(mc.Arguments[0]);

                if (left is MemberExpression leftMember)
                {
                    EnsureDirectParameterAccess(leftMember);
                    var leftSql = ResolveColumn(leftMember, ctx);
                    var rightSql = ctx.AddParam(Evaluate(right));
                    sql = ctx.Dialect.EqualsIgnoreCase(leftSql, rightSql);
                    return true;
                }

                return false;
            }
        }

        if (mc.Object is null && mc.Method.DeclaringType == typeof(string))
        {
            if (mc.Arguments.Count == 3 &&
                mc.Arguments[2].Type == typeof(StringComparison) &&
                TryEvalStringComparison(mc.Arguments[2], out var sc) &&
                sc == StringComparison.OrdinalIgnoreCase)
            {
                var a = StripConvert(mc.Arguments[0]);
                var b = StripConvert(mc.Arguments[1]);

                if (a is MemberExpression aMember)
                {
                    EnsureDirectParameterAccess(aMember);
                    var leftSql = ResolveColumn(aMember, ctx);
                    var rightSql = ctx.AddParam(Evaluate(b));
                    sql = ctx.Dialect.EqualsIgnoreCase(leftSql, rightSql);
                    return true;
                }

                if (b is MemberExpression bMember)
                {
                    EnsureDirectParameterAccess(bMember);
                    var leftSql = ResolveColumn(bMember, ctx);
                    var rightSql = ctx.AddParam(Evaluate(a));
                    sql = ctx.Dialect.EqualsIgnoreCase(leftSql, rightSql);
                    return true;
                }

                return false;
            }
        }
    }

    return false;
}

private static bool TryEvalStringComparison(Expression expr, out StringComparison sc)
{
    expr = StripConvert(expr);

    if (expr is ConstantExpression ce && ce.Value is StringComparison s)
    {
        sc = s;
        return true;
    }

    try
    {
        var v = Evaluate(expr);
        if (v is StringComparison s2)
        {
            sc = s2;
            return true;
        }
    }
    catch { }

    sc = default;
    return false;
}

private static void EnsureDirectParameterAccess(MemberExpression member)
{
    var inner = StripConvert(member.Expression!);
    if (inner is not ParameterExpression)
        throw new NotSupportedException();
}

}
