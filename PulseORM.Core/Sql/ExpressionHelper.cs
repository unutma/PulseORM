using System.Linq.Expressions;
using System.Reflection;

namespace PulseORM.Core.Sql;

internal static class ExpressionHelper
{
    internal static MemberInfo ExtractMember(Expression expr)
    {
        if (expr is UnaryExpression u && expr.NodeType == ExpressionType.Convert)
            return ExtractMember(u.Operand);

        if (expr is MemberExpression m)
            return m.Member;

        throw new InvalidOperationException($"Unsupported expression: {expr.NodeType} ({expr})");
    }
}
