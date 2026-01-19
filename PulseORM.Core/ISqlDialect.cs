namespace PulseORM.Core;

public interface ISqlDialect
{
    string Param(string name);
    string ApplyPagination(
        string sql,
        int skip,
        int take,
        string orderBySql);
    string BoolLiteral(bool value);
    string EqualsIgnoreCase(string leftSql, string rightSql);
    string LikeIgnoreCase(string leftSql, string rightSql);


}