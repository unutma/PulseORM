using PulseORM.Core;

namespace PulseORM.DemoDataLayer;

public interface IPulseDbContext
{
    PulseLiteDb Db { get; }
}