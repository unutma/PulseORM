using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace PulseORM.Core;

public sealed class OracleConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public OracleConnectionFactory(string connectionString)
        => _connectionString = connectionString;

    public IDbConnection Create()
        => new OracleConnection(_connectionString);
}