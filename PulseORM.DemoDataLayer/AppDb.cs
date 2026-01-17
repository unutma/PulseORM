using PulseORM.Core;
using PulseORM.Core.Sql;
using PulseORM.DemoDataLayer;

public sealed class AppDb : IAppDb
{
    private readonly PulseLiteDb _db;

    public AppDb(IPulseDbContext context)
    {
        _db = context.Db;
    }

    public Query<T> Query<T>() where T : new() => _db.Query<T>();

    public PulseQueryJoin<T> QueryJoin<T>() where T : new() => _db.QueryJoin<T>();

    public SqlQuery<T> SqlQuery<T>(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
        where T : new()
        => _db.SqlQuery<T>(sql, parameters);

    public Task<T?> GetByIdAsync<T>(object id) where T : new() => _db.GetByIdAsync<T>(id);

    public Task<List<T>> GetAllAsync<T>() where T : new() => _db.GetAllAsync<T>();

    public Task<int> InsertAsync<T>(T entity) where T : new() => _db.InsertAsync(entity);

    public Task<int> UpdateAsync<T>(T entity) where T : new() => _db.UpdateAsync(entity);

    public Task<int> DeleteAsync<T>(T entity) where T : new() => _db.DeleteAsync(entity);

    public Task<int> DeleteByIdAsync<T>(object id) where T : new() => _db.DeleteByIdAsync<T>(id);

    public Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, int batchSize = 500) where T : new()
        => _db.BulkInsertAsync(entities, batchSize);

    public Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, int batchSize = 500) where T : new()
        => _db.BulkUpdateAsync(entities, batchSize);
}