using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using PulseORM.Core.Sql;

namespace PulseORM.Core;

public partial class PulseLiteDb
{ 
    private async Task<int> BulkInsertInternalAsync<T>(
        IList<T> entities,
        int batchSize)
        where T : new()
    {
        var map = ModelMapper.GetMap<T>();
        var total = 0;

        using var conn = _factory.Create();
        await OpenAsync(conn);

        using var tx = conn.BeginTransaction();

        for (int offset = 0; offset < entities.Count; offset += batchSize)
        {
            var batch = entities.Skip(offset).Take(batchSize).ToList();
            var spec = BuildBulkInsertSpec(batch, map);
            total += await ExecuteBulkAsync(conn, tx, spec);
        }

        tx.Commit();
        return total;
    }
    
    private CommandSpec BuildBulkInsertSpec<T>(
        IList<T> batch,
        EntityMap map)
        where T : new()
    {
        var columns = new List<PropertyMap>();

        foreach (var p in map.Properties)
        {
            if (ReferenceEquals(p.PropertyInfo, map.Key.PropertyInfo))
            {
                var keyVal = p.PropertyInfo.GetValue(batch[0]);
                if (IsDefaultValue(keyVal, p.PropertyInfo.PropertyType))
                    continue;
            }

            columns.Add(p);
        }

        if (columns.Count == 0)
            throw new InvalidOperationException($"No columns to insert for {map.Type.Name}.");

        var sqlCols = string.Join(", ", columns.Select(c => c.ColumnName));
        var valuesSql = new List<string>();
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var pIndex = 0;

        foreach (var entity in batch)
        {
            var valueParams = new List<string>();

            foreach (var col in columns)
            {
                var name = $"p{pIndex++}";
                valueParams.Add(_dialect.Param(name));
                parameters[name] = col.PropertyInfo.GetValue(entity);
            }

            valuesSql.Add($"({string.Join(", ", valueParams)})");
        }

        var sql =
            $"INSERT INTO {map.TableName} ({sqlCols}) VALUES {string.Join(", ", valuesSql)}";

        return new CommandSpec(sql, parameters);
    }

    private async Task<int> BulkUpdateInternalAsync<T, TKey, TUpdate>(
        IList<T> entities,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TUpdate>> updateSelector,
        int batchSize)
        where T : new()
    {
        var map = ModelMapper.GetMap<T>();
        var total = 0;

        using var conn = _factory.Create();
        await OpenAsync(conn);

        using var tx = conn.BeginTransaction();

        for (int offset = 0; offset < entities.Count; offset += batchSize)
        {
            var batch = entities.Skip(offset).Take(batchSize).ToList();
            var spec = BuildBulkUpdateSpec(batch, map, keySelector, updateSelector);
            total += await ExecuteBulkAsync(conn, tx, spec);
        }

        tx.Commit();
        return total;
    }

    private async Task<long> QueryCountAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        using var conn = _factory.Create();
        await OpenAsync(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        var result = await ExecuteScalarLongAsync(sql, parameters);

        return result;
    }
    
    private CommandSpec BuildInsertSpec<T>(T entity) where T : new()
    {
        var map = ModelMapper.GetMap<T>();

        var cols = new List<string>();
        var vals = new List<string>();
        var ps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var i = 0;
        
        var keyInfo = map.Key?.PropertyInfo;
        if (keyInfo == null)
            throw new InvalidOperationException(
                $"No key mapping found for entity '{typeof(T).Name}'. Add [Key] or use 'Id' / '{typeof(T).Name}Id'.");

        foreach (var p in map.Properties)
        {
            if (ReferenceEquals(p.PropertyInfo, map.Key.PropertyInfo))
            {
                var keyVal = p.PropertyInfo.GetValue(entity);
                if (IsDefaultValue(keyVal, p.PropertyInfo.PropertyType))
                    continue;
            }

            var paramName = _dialect.Param($"p{i++}");
            cols.Add(p.ColumnName);
            vals.Add(paramName);
            ps[paramName] = p.PropertyInfo.GetValue(entity);
        }

        if (cols.Count == 0)
            throw new InvalidOperationException($"No mapped columns to insert for {typeof(T).Name}.");

        var sql = $"INSERT INTO {map.TableName} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
        return new CommandSpec(sql, ps);
    }
    
    private async Task<int> ExecuteBulkAsync(
        IDbConnection conn,
        IDbTransaction tx,
        CommandSpec spec)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = spec.Sql;

        AddParameters(cmd, spec.Parameters);

        if (cmd is DbCommand dbCmd)
            return await dbCmd.ExecuteNonQueryAsync();

        return cmd.ExecuteNonQuery();
    }
        
    private CommandSpec BuildBulkUpdateSpec<T, TKey, TUpdate>(
        IList<T> batch,
        EntityMap map,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TUpdate>> updateSelector)
    {
        var keyProp = GetProperty(keySelector);
        var updateProps = GetProperties(updateSelector);

        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var setClauses = new List<string>();
        var inParams = new List<string>();

        var idx = 0;

        foreach (var prop in updateProps)
        {
            var cases = new List<string>();

            foreach (var e in batch)
            {
                var keyName = $"k{idx}";
                var valName = $"v{idx}";

                cases.Add($"WHEN {_dialect.Param(keyName)} THEN {_dialect.Param(valName)}");

                parameters[keyName] = keyProp.GetValue(e);
                parameters[valName] = prop.GetValue(e);

                idx++;
            }

            setClauses.Add(
                $"{prop.Name} = CASE {keyProp.Name} {string.Join(" ", cases)} END"
            );
        }

        foreach (var e in batch)
        {
            var name = $"in{idx++}";
            inParams.Add(_dialect.Param(name));
            parameters[name] = keyProp.GetValue(e);
        }

        var sql =
            $"UPDATE {map.TableName} SET {string.Join(", ", setClauses)} " +
            $"WHERE {keyProp.Name} IN ({string.Join(", ", inParams)})";

        return new CommandSpec(sql, parameters);
    }

    
    private static PropertyInfo GetProperty<T, TValue>(
        Expression<Func<T, TValue>> expr)
    {
        if (expr.Body is MemberExpression m && m.Member is PropertyInfo pi)
            return pi;

        throw new InvalidOperationException("Expression must be a property.");
    }

    private static List<PropertyInfo> GetProperties<T, TValue>(
        Expression<Func<T, TValue>> expr)
    {
        if (expr.Body is NewExpression n)
            return n.Members!.Cast<PropertyInfo>().ToList();

        throw new InvalidOperationException("Update selector must be anonymous object.");
    }

    private CommandSpec BuildUpdateSpec<T>(T entity) where T : new()
    {
        var map = ModelMapper.GetMap<T>();

        var sets = new List<string>();
        var ps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var i = 0;

        foreach (var p in map.Properties)
        {
            if (ReferenceEquals(p.PropertyInfo, map.Key.PropertyInfo))
                continue;

            var paramName = _dialect.Param($"p{i++}");
            sets.Add($"{p.ColumnName} = {paramName}");
            ps[paramName] = p.PropertyInfo.GetValue(entity);
        }

        if (sets.Count == 0)
            throw new InvalidOperationException($"No updatable columns mapped for {typeof(T).Name}.");

        var keyParam = _dialect.Param("key");
        ps[keyParam] = map.Key.PropertyInfo.GetValue(entity);

        var sql = $"UPDATE {map.TableName} SET {string.Join(", ", sets)} WHERE {map.Key.ColumnName} = {keyParam}";
        return new CommandSpec(sql, ps);
    }

    private CommandSpec BuildDeleteByEntitySpec<T>(T entity) where T : new()
    {
        var map = ModelMapper.GetMap<T>();

        var keyParam = _dialect.Param("key");
        var ps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [keyParam] = map.Key.PropertyInfo.GetValue(entity)
        };

        var sql = $"DELETE FROM {map.TableName} WHERE {map.Key.ColumnName} = {keyParam}";
        return new CommandSpec(sql, ps);
    }

    private CommandSpec BuildDeleteByIdSpec<T>(object id) where T : new()
    {
        var map = ModelMapper.GetMap<T>();

        var keyParam = _dialect.Param("key");
        var ps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [keyParam] = id
        };

        var sql = $"DELETE FROM {map.TableName} WHERE {map.Key.ColumnName} = {keyParam}";
        return new CommandSpec(sql, ps);
    }

    private static bool IsDefaultValue(object? value, Type memberType)
    {
        if (value is null)
            return true;

        var t = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (!t.IsValueType)
            return false;

        var def = Activator.CreateInstance(t);
        return value.Equals(def);
    }

   

    internal async Task<IDataReader> QueryJoinRowsAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var conn = _factory.Create();
        await OpenAsync(conn);

        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        var reader = await ExecuteReaderAsync(cmd);
        return new ConnectionOwnedReader(conn, cmd, reader);
    }

    private sealed class ConnectionOwnedReader : IDataReader
    {
        private readonly IDisposable _conn;
        private readonly IDisposable _cmd;
        private readonly IDataReader _inner;

        public ConnectionOwnedReader(IDisposable conn, IDisposable cmd, IDataReader inner)
        {
            _conn = conn; _cmd = cmd; _inner = inner;
        }

        public void Dispose()
        {
            _inner.Dispose();
            _cmd.Dispose();
            _conn.Dispose();
        }

        public bool Read() => _inner.Read();
        public int FieldCount => _inner.FieldCount;
        public object GetValue(int i) => _inner.GetValue(i);
        public string GetName(int i) => _inner.GetName(i);
        public bool IsDBNull(int i) => _inner.IsDBNull(i);
        public void Close() => _inner.Close();

        public object this[int i] => _inner[i];
        public object this[string name] => _inner[name];

        public int Depth => _inner.Depth;
        public bool IsClosed => _inner.IsClosed;
        public int RecordsAffected => _inner.RecordsAffected;

        public bool NextResult() => _inner.NextResult();
        public DataTable GetSchemaTable() => _inner.GetSchemaTable();

        public bool GetBoolean(int i) => _inner.GetBoolean(i);
        public byte GetByte(int i) => _inner.GetByte(i);
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => _inner.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
        public char GetChar(int i) => _inner.GetChar(i);
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => _inner.GetChars(i, fieldoffset, buffer, bufferoffset, length);
        public IDataReader GetData(int i) => _inner.GetData(i);
        public string GetDataTypeName(int i) => _inner.GetDataTypeName(i);
        public DateTime GetDateTime(int i) => _inner.GetDateTime(i);
        public decimal GetDecimal(int i) => _inner.GetDecimal(i);
        public double GetDouble(int i) => _inner.GetDouble(i);
        public Type GetFieldType(int i) => _inner.GetFieldType(i);
        public float GetFloat(int i) => _inner.GetFloat(i);
        public Guid GetGuid(int i) => _inner.GetGuid(i);
        public short GetInt16(int i) => _inner.GetInt16(i);
        public int GetInt32(int i) => _inner.GetInt32(i);
        public long GetInt64(int i) => _inner.GetInt64(i);
        public int GetOrdinal(string name) => _inner.GetOrdinal(name);
        public string GetString(int i) => _inner.GetString(i);
        public int GetValues(object[] values) => _inner.GetValues(values);

        public int GetHashCode() => _inner.GetHashCode();
        public bool Equals(object? obj) => _inner.Equals(obj);
        public string? ToString() => _inner.ToString();

        public void Reset() => throw new NotSupportedException();

        public IDataReader GetDataReader() => _inner;
    }

    private static string NormalizeOrderBy(string orderBySql)
    {
        var s = orderBySql.Trim();
        return s.StartsWith("ORDER BY", StringComparison.OrdinalIgnoreCase)
            ? s
            : $"ORDER BY {s}";
    }

    private async Task<T?> GetSingleAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
        where T : new()
    {
        var list = await QueryAsync<T>(sql, parameters);
        return list.FirstOrDefault();
    }
}
