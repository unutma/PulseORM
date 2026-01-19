using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

public partial class PulseLiteDb
{
    public Query<T> Query<T>() where T : new()
        => new Query<T>(this);
    
    public SqlQuery<T> SqlQuery<T>(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
        where T : new()
    {
        return new SqlQuery<T>(this, sql, parameters);
    }

    

    public PulseQueryJoin<T> QueryJoin<T>() where T : new()
        => new PulseQueryJoin<T>(this);

    public Task<int> InsertAsync<T>(T entity) where T : new()
        => ExecuteAsync(BuildInsertSpec(entity));

    public Task<int> UpdateAsync<T>(T entity) where T : new()
        => ExecuteAsync(BuildUpdateSpec(entity));
    
    public Task<int> DeleteByIdAsync<T>(object id) where T : new()
        => ExecuteAsync(BuildDeleteByIdSpec<T>(id));

    public Task<int> DeleteAsync<T>(T entity) where T : new()
        => ExecuteAsync(BuildDeleteByEntitySpec(entity));
    private readonly IDbConnectionFactory _factory;
    internal ISqlDialect _dialect { get; }
    
    private static readonly IReadOnlyDictionary<string, object?> EmptyParameters
        = new Dictionary<string, object?>();
    public PulseLiteDb(IDbConnectionFactory factory, ISqlDialect dialect)
    {
        _factory = factory;
        _dialect = dialect;
    }
    
    internal async Task<int> ExecuteAsync(CommandSpec spec)
    {
        using var conn = _factory.Create();
        await OpenAsync(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = spec.Sql;
        AddParameters(cmd, spec.Parameters);
        return await ExecuteNonQueryAsync(cmd);
    }
    
    internal async Task<List<T>> QueryAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters)
        where T : new()
    {
        using var conn = _factory.Create();
        await OpenAsync(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        using var reader = await ExecuteReaderAsync(cmd);

        return Materializer.Materialize<T>(reader);
    }
    
    public Task<int> BulkInsertAsync<T>(
        IEnumerable<T> entities,
        int batchSize = 500)
        where T : new()
    {
        if (entities is null)
            throw new ArgumentNullException(nameof(entities));

        var list = entities as IList<T> ?? entities.ToList();
        if (list.Count == 0)
            return Task.FromResult(0);

        return BulkInsertInternalAsync(list, batchSize);
    }
    
    public Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, int batchSize = 500)
        where T : new()
    {
        if (entities is null)
            throw new ArgumentNullException(nameof(entities));

        var list = entities as IList<T> ?? entities.ToList();
        if (list.Count == 0)
            return Task.FromResult(0);

        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        return UpdateManyInternalAsync(list, batchSize);
    }

    private async Task<int> UpdateManyInternalAsync<T>(IList<T> entities, int batchSize)
        where T : new()
    {
        var total = 0;

        using var conn = _factory.Create();
        await OpenAsync(conn);

        using var tx = conn.BeginTransaction();

        for (int offset = 0; offset < entities.Count; offset += batchSize)
        {
            var take = Math.Min(batchSize, entities.Count - offset);

            for (int i = 0; i < take; i++)
            {
                var entity = entities[offset + i];
                var spec = BuildUpdateSpec(entity);

                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = spec.Sql;
                AddParameters(cmd, spec.Parameters);

                total += await ExecuteNonQueryAsync(cmd);
            }
        }

        tx.Commit();
        return total;
    }

    
    
    
    public Task<T?> GetByIdAsync<T>(object id) where T : new()
    {
        var map = ModelMapper.GetMap<T>();
        var idParam = _dialect.Param("id");
        var sql = $"SELECT * FROM {map.TableName} WHERE {map.Key.ColumnName} = {idParam}";

        var parameters = new Dictionary<string, object?>
        {
            [idParam] = id
        };

        return GetSingleAsync<T>(sql, parameters);
    }

    public Task<List<T>> QueryPagedAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        int page,
        int pageSize,
        string orderBySql)
        where T : new()
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var skip = (page - 1) * pageSize;
        var pagedSql = _dialect.ApplyPagination(sql, skip, pageSize, orderBySql);

        return QueryAsync<T>(pagedSql, parameters);
    }
    
    public Task<List<T>> GetAllAsync<T>() where T : new()
    {
        var map = ModelMapper.GetMap<T>();
        var sql = $"SELECT * FROM {map.TableName}";
        var parameters = new Dictionary<string, object?>();
        return QueryAsync<T>(sql, parameters);
    }
    
    public async Task<(List<T> Items, long TotalCount)> GetAllPagedAsync<T>(
        int page,
        int pageSize,
        Expression<Func<T, object>>? orderBy,
        bool descending = false,
        Expression<Func<T, bool>>? whereInclude = null)
        where T : new()
    {
        var map = ModelMapper.GetMap<T>();
        var sql = $"SELECT * FROM {map.TableName}";
        var countSql = $"SELECT COUNT(*) FROM {map.TableName}";
        var parameters = new Dictionary<string, object?>();

        if (whereInclude is not null)
        {
            var where = WhereBuilder.Build(whereInclude, map, _dialect);

            if (!string.IsNullOrWhiteSpace(where.Sql))
            {
                sql += " WHERE " + where.Sql;
                countSql += " WHERE " + where.Sql;
            }

            foreach (var kv in where.Parameters)
                parameters[kv.Key] = kv.Value;
        }

        var totalCount = await QueryCountAsync(countSql, parameters);
        var orderBySql = OrderByBuilder.Build(orderBy, map, descending);

        var items = await QueryPagedAsync<T>(sql, parameters, page, pageSize, orderBySql);

        return (items, totalCount);
    }

    
    internal Task<long> QueryCountSqlAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
        => QueryCountSqlCoreAsync(sql, parameters ?? EmptyParameters);

    private Task<long> QueryCountSqlAsync(
        string sql,
        params (string Key, object? Value)[] parameters)
    {
        if (parameters is null || parameters.Length == 0)
            return QueryCountSqlCoreAsync(sql, EmptyParameters);

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in parameters)
            dict[k] = v;

        return QueryCountSqlCoreAsync(sql, dict);
    }
    
    public async Task<List<TScalar>> QueryScalarListAsync<TScalar>(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        using var conn = _factory.Create();
        await OpenAsync(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        using var reader = await ExecuteReaderAsync(cmd).ConfigureAwait(false);

        var list = new List<TScalar>();
        while (reader.Read())
        {
            var v = reader.GetValue(0);
            if (v is null || v is DBNull)
            {
                list.Add(default!);
                continue;
            }

            if (v is TScalar typed)
            {
                list.Add(typed);
                continue;
            }

            list.Add((TScalar)Convert.ChangeType(
                v,
                Nullable.GetUnderlyingType(typeof(TScalar)) ?? typeof(TScalar)));
        }

        return list;
    }
    public async Task<long> QueryCountSqlCoreAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        using var conn = _factory.Create();
        await OpenAsync(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        var result = cmd.ExecuteScalar();

        return result is null || result is DBNull
            ? 0L
            : Convert.ToInt64(result);
    }
    
    private static void AddParameters(IDbCommand cmd, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return;
        foreach (var kvp in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = kvp.Key;            
            p.Value = kvp.Value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }

    private static Task OpenAsync(IDbConnection conn)
    {
        if (conn is System.Data.Common.DbConnection dbConn)
        {
            return dbConn.OpenAsync();
        }
        conn.Open();
        return Task.CompletedTask;
    }

    private static Task<int> ExecuteNonQueryAsync(IDbCommand cmd)
    {
        if (cmd is System.Data.Common.DbCommand dbCmd)
        {
            return dbCmd.ExecuteNonQueryAsync();
        }
        
        return Task.FromResult(cmd.ExecuteNonQuery());
    }
    
    private async Task<long> ExecuteScalarLongAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create(); 
        await OpenAsync(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        var result =  cmd.ExecuteScalar();
        return result is null || result is DBNull ? 0L : Convert.ToInt64(result);
    }

    
    private static Task<IDataReader> ExecuteReaderAsync(IDbCommand cmd)
    {
        if (cmd is System.Data.Common.DbCommand dbCmd)
        {
            return dbCmd.ExecuteReaderAsync()
                .ContinueWith<IDataReader>(t => t.Result);
        }

        return Task.FromResult(cmd.ExecuteReader());
    }
    
    private static Task<bool> ReadAsync(IDataReader reader)
    {
        if (reader is System.Data.Common.DbDataReader dbReader)
        {
            return dbReader.ReadAsync();
        }

        return Task.FromResult(reader.Read());
    }
    
    private async Task<TScalar> ExecuteScalarAsync<TScalar>(
        string sql,
        IDictionary<string, object?> parameters,
        ISqlDialect dialect,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL cannot be empty.", nameof(sql));

        await using var connection = (DbConnection)_factory.Create();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        foreach (var kv in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = dialect.Param(kv.Key);
            p.Value = kv.Value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        var result = await cmd.ExecuteScalarAsync(ct);

        if (result is null || result is DBNull)
            return default!;

        var targetType = Nullable.GetUnderlyingType(typeof(TScalar)) ?? typeof(TScalar);
        return (TScalar)Convert.ChangeType(result, targetType, CultureInfo.InvariantCulture);
    }

        private static string RemoveTopLevelOrderBy(string sql)
        {
            var depth = 0;

            for (int i = 0; i <= sql.Length - 8; i++)
            {
                var ch = sql[i];

                if (ch == '(') depth++;
                else if (ch == ')') depth--;

                if (depth == 0 &&
                    sql.AsSpan(i).StartsWith("ORDER BY", StringComparison.OrdinalIgnoreCase))
                {
                    return sql.Substring(0, i).TrimEnd();
                }
            }

            return sql;
        }
    }
    