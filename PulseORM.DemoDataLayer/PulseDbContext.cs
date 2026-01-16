using Microsoft.Extensions.Configuration;
using PulseORM.Core;

namespace PulseORM.DemoDataLayer;

public sealed class PulseDbContext : IPulseDbContext
{
    public PulseLiteDb Db { get; }

    public PulseDbContext(IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("Default");

        var factory = new NpgsqlConnectionFactory(cs);
        var dialect = new PostgresDialect();

        Db = new PulseLiteDb(factory, dialect);
    }
}