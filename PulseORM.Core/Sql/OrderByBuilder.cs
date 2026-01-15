using System.Linq.Expressions;

namespace PulseORM.Core.Sql;

internal static class OrderByBuilder
{
    public static string Build<T>(
        Expression<Func<T, object>>? orderBy,
        EntityMap map,
        bool descending,
        string? tableAlias = null)
    {
        if (orderBy is not null)
        {
            var col = ExtractColumnName(orderBy.Body, map);
            var qualified = string.IsNullOrWhiteSpace(tableAlias) ? col : $"{tableAlias}.{col}";
            return $"ORDER BY {qualified} {(descending ? "DESC" : "ASC")}";
        }

        var key = map.Key?.ColumnName;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"Pagination requires ORDER BY. No key mapped for {typeof(T).Name}. " +
                "Pass orderBy expression (e.g. x => x.CreatedAt).");

        var qualifiedKey = string.IsNullOrWhiteSpace(tableAlias) ? key : $"{tableAlias}.{key}";
        return $"ORDER BY {qualifiedKey} ASC";
    }

    private static string ExtractColumnName(Expression expr, EntityMap map)
    {
        expr = StripConvert(expr);

        if (expr is MemberExpression member)
        {
            if (StripConvert(member.Expression) is not ParameterExpression)
                throw new NotSupportedException(
                    "OrderBy must be a direct property access like: x => x.CreatedAt");

            var propName = member.Member.Name;

            if (!map.PropertyByName.TryGetValue(propName, out var pm))
                throw new InvalidOperationException(
                    $"Property '{propName}' is not mapped for table '{map.TableName}'.");

            return pm.ColumnName;
        }

        throw new NotSupportedException(
            "OrderBy expression must be a simple property access like: x => x.CreatedAt");
    }

    private static Expression StripConvert(Expression expr)
    {
        while (expr is UnaryExpression u &&
               (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked))
        {
            expr = u.Operand;
        }
        return expr;
    }
}
