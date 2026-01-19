using System.Linq.Expressions;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

public interface IAppDb
{
    Query<T> Query<T>() where T : new();

    SqlQuery<T> SqlQuery<T>(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
        where T : new();

    PulseQueryJoin<T> QueryJoin<T>() where T : new();

    Task<int> InsertAsync<T>(T entity) where T : new();
    Task<int> UpdateAsync<T>(T entity) where T : new();

    Task<int> DeleteByIdAsync<T>(object id) where T : new();
    Task<int> DeleteAsync<T>(T entity) where T : new();

    Task<int> BulkInsertAsync<T>(
        IEnumerable<T> entities,
        int batchSize = 500)
        where T : new();

    Task<int> BulkUpdateAsync<T>(
        IEnumerable<T> entities,
        int batchSize = 500)
        where T : new();

    Task<T?> GetByIdAsync<T>(object id) where T : new();

    Task<List<T>> GetAllAsync<T>() where T : new();

    Task<List<T>> QueryPagedAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        int page,
        int pageSize,
        string orderBySql)
        where T : new();

    Task<(List<T> Items, long TotalCount)> GetAllPagedAsync<T>(
        int page,
        int pageSize,
        Expression<Func<T, object>>? orderBy,
        bool descending = false,
        Expression<Func<T, bool>>? whereInclude = null)
        where T : new();

    Task<List<TScalar>> QueryScalarListAsync<TScalar>(
        string sql,
        IReadOnlyDictionary<string, object?> parameters);

    Task<long> QueryCountSqlCoreAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters);
}