namespace PulseORM.Core;

public sealed class PostgresDialect : ISqlDialect
{
    public string Param(string name) => "@" + name;

    public string ApplyPagination(
        string sql,
        int skip,
        int take,
        string orderBySql)
        => $"{sql} {orderBySql} LIMIT {take} OFFSET {skip}";
    
    public string BoolLiteral(bool value)
        => value ? "TRUE" : "FALSE";

    public string EqualsIgnoreCase(string leftSql, string rightSql)
        => $"({leftSql} ILIKE {rightSql})";

    public string LikeIgnoreCase(string leftSql, string rightSql)
        => $"({leftSql} ILIKE {rightSql})";
}


public sealed class OracleDialect  : ISqlDialect
{
    public string Param(string name) => ":" + name;
    
    public string ApplyPagination(
        string sql,
        int skip,
        int take,
        string orderBySql)
        => $"{sql} {orderBySql} OFFSET {skip} ROWS FETCH NEXT {take} ROWS ONLY";
    
    public string BoolLiteral(bool value)
        => value ? "1" : "0";
    
    public string EqualsIgnoreCase(string leftSql, string rightSql)
        => $"(UPPER({leftSql}) = UPPER({rightSql}))";

    public string LikeIgnoreCase(string leftSql, string rightSql)
        => $"({leftSql} LIKE {rightSql})";
}

public sealed class SqlServerDialect : ISqlDialect
{
    public string Param(string name) => "@" + name;
    public string ApplyPagination(
        string sql,
        int skip,
        int take,
        string orderBySql)
        => $"{sql} {orderBySql} OFFSET {skip} ROWS FETCH NEXT {take} ROWS ONLY";
    
    public string BoolLiteral(bool value)
        => value ? "1" : "0";
    
    public string EqualsIgnoreCase(string leftSql, string rightSql)
        => $"(UPPER({leftSql}) = UPPER({rightSql}))";

    public string LikeIgnoreCase(string leftSql, string rightSql)
        => $"({leftSql} LIKE {rightSql})";

}