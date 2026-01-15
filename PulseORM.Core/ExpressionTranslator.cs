using System.Linq.Expressions;

namespace PulseORM.Core;

internal class ExpressionTranslator<T> where T : new()
{
    private readonly EntityMap _map;
    private readonly Dictionary<string, object?> _parameters;
    private readonly ISqlDialect _sqlDialect;
    private int _paramIndex = 0;

    public ExpressionTranslator(EntityMap map, Dictionary<string, object?> parameters, ISqlDialect sqlDialect)
    {
        _map = map;
        _parameters = parameters;
        _sqlDialect = sqlDialect;
    }
    
    public string Translate(Expression expression)
    {
        return Visit(expression);
    }
    
    private string NextParam(object? value)
    {
        var name = $"p{_paramIndex++}";
        var p = _sqlDialect.Param(name);
        _parameters[p] = value;
        return p;
    }

    private string Visit(Expression exp)
    {
        return exp switch
        {
            BinaryExpression b      => VisitBinary(b),
            MemberExpression m      => VisitMember(m),
            ConstantExpression c    => VisitConstant(c),
            MethodCallExpression mc => VisitMethodCall(mc),

            _ => throw new NotSupportedException($"Unsupported expression: {exp.NodeType}")
        };
    }

    private string VisitBinary(BinaryExpression b)
    {
        var left = Visit(b.Left);
        var right = Visit(b.Right);

        var op = b.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            _ => throw new NotSupportedException($"Unsupported operator: {b.NodeType}")
        };

        return $"{left} {op} {right}";
    }

    private string VisitMember(MemberExpression m)
    {
        var prop = _map.Properties.FirstOrDefault(p => p.PropertyInfo.Name == m.Member.Name);
        if (prop == null)
            throw new NotSupportedException($"Unknown member: {m.Member.Name}");

        return prop.ColumnName;
    }

    private string VisitConstant(ConstantExpression c)
    {
        return NextParam(c.Value);
    }

    private string VisitMethodCall(MethodCallExpression mc)
    {
        if (mc.Method.Name == nameof(string.StartsWith))
        {
            var member = (MemberExpression)mc.Object!;
            var prop = _map.Properties.First(p => p.PropertyInfo.Name == member.Member.Name);

            var argValue = (string)((ConstantExpression)mc.Arguments[0]).Value!;
            var paramName = $"@p{_paramIndex++}";
            _parameters[paramName] = argValue + "%";

            return $"{prop.ColumnName} LIKE {paramName}";
        }

        throw new NotSupportedException($"Method not supported: {mc.Method.Name}");
    }
}
