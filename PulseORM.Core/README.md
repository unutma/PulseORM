# PulseORM.Core

PulseORM.Core is a lightweight ORM for .NET 8 and above.
It aims to reduce the amount of bare SQL you need to write compared to Dapper, remains intentionally minimal compared to EF Core, and targets Dapper-like performance.

## Features

- .NET 8+ support
- Expression-based filtering
- CRUD operations
- Bulk insert and bulk update
- Pagination support
- Join pipeline with projection
- Multi-dialect support (PostgreSQL, SQL Server, Oracle)

## Quick Example

dotnet add package PulseORM.Core --version 1.0.0

```csharp
using PulseORM.Core;

var factory = new NpgsqlConnectionFactory(connectionString);
var dialect = new PostgresDialect();

var db = new PulseLiteDb(factory, dialect);

var users = await db.Query<User>()
    .FilterSql(x => x.IsActive)
    .ToListAsync();
```

## Dialects

- `PostgresDialect`
- `SqlServerDialect`
- `OracleDialect`

## Repository

GitHub: https://github.com/unutma/PulseORM
