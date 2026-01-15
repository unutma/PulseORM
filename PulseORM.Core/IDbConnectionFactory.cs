using System.Data;

namespace PulseORM.Core;

public interface IDbConnectionFactory
{
        IDbConnection Create();
}