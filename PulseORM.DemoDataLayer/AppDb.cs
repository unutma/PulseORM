using System.Linq.Expressions;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

public sealed class AppDb : IAppDb
{
    private readonly PulseLiteDb _db;
    public AppDb(PulseLiteDb db) => _db = db;

    public Query<T> Query<T>() where T : new() => _db.Query<T>();

    public SqlQuery<T> SqlQuery<T>(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
        where T : new()
        => _db.SqlQuery<T>(sql, parameters);

    public PulseQueryJoin<T> QueryJoin<T>() where T : new() => _db.QueryJoin<T>();

    public Task<int> InsertAsync<T>(T entity) where T : new() => _db.InsertAsync(entity);
    public Task<int> UpdateAsync<T>(T entity) where T : new() => _db.UpdateAsync(entity);

    public Task<int> DeleteByIdAsync<T>(object id) where T : new() => _db.DeleteByIdAsync<T>(id);
    public Task<int> DeleteAsync<T>(T entity) where T : new() => _db.DeleteAsync(entity);

    public Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, int batchSize = 500) where T : new()
        => _db.BulkInsertAsync(entities, batchSize);

    public Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, int batchSize = 500) where T : new()
        => _db.BulkUpdateAsync(entities, batchSize);

    public Task<T?> GetByIdAsync<T>(object id) where T : new() => _db.GetByIdAsync<T>(id);
    public Task<List<T>> GetAllAsync<T>() where T : new() => _db.GetAllAsync<T>();

    public Task<List<T>> QueryPagedAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        int page,
        int pageSize,
        string orderBySql)
        where T : new()
        => _db.QueryPagedAsync<T>(sql, parameters, page, pageSize, orderBySql);

    public Task<(List<T> Items, long TotalCount)> GetAllPagedAsync<T>(
        int page,
        int pageSize,
        Expression<Func<T, object>>? orderBy,
        bool descending = false,
        Expression<Func<T, bool>>? whereInclude = null)
        where T : new()
        =>
            _db.GetAllPagedAsync<T>(page, pageSize, orderBy, descending, whereInclude);

    public Task<List<TScalar>> QueryScalarListAsync<TScalar>(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
        => _db.QueryScalarListAsync<TScalar>(sql, parameters);

    public Task<long> QueryCountSqlCoreAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
        => _db.QueryCountSqlCoreAsync(sql, parameters);
}
