using PulseORM.Core;
using PulseORM.Core.Sql;

public interface IAppDb
{
    Query<T> Query<T>() where T : new();
    PulseQueryJoin<T> QueryJoin<T>() where T : new();

    SqlQuery<T> SqlQuery<T>(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
        where T : new();
    
    Task<T?> GetByIdAsync<T>(object id) where T : new();
    Task<List<T>> GetAllAsync<T>() where T : new();

    Task<int> InsertAsync<T>(T entity) where T : new();
    Task<int> UpdateAsync<T>(T entity) where T : new();
    Task<int> DeleteAsync<T>(T entity) where T : new();
    Task<int> DeleteByIdAsync<T>(object id) where T : new();
    Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, int batchSize = 500) where T : new();
    Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, int batchSize = 500) where T : new();
}