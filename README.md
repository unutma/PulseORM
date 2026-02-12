# PulseORM

PulseORM is a lightweight, SQL-first ORM for .NET.
It focuses on explicit SQL generation, predictable mapping, and minimal abstraction over ADO.NET.

## Highlights

- Lightweight query API with expression-based filtering
- CRUD operations (`Insert`, `Update`, `Delete`, `GetById`)
- Bulk operations (`BulkInsert`, `BulkUpdate`)
- Pagination support
- Join pipeline with projection support
- Raw SQL entry point with typed materialization
- Multi-dialect support:
  - PostgreSQL
  - SQL Server
  - Oracle

## Project Structure

- `PulseORM.Core`: Core ORM library

 ** This is an example Project. Please Check it
- `PulseORM.Entities`: Shared entities/models
- `PulseORM.DemoEntities`: Demo entity models
- `PulseORM.DemoDataLayer`: Demo data access layer
- `PulseORM.DemoService`: Demo service layer
- `PulseORM.DemoApi`: Demo API application

## Installation

Clone the repository and restore dependencies:

```bash
git clone <https://github.com/unutma/PulseORM.git>
cd PulseORM
dotnet restore
```

## Quick Start

```csharp
using PulseORM.Core;

var factory = new NpgsqlConnectionFactory(connectionString);
var dialect = new PostgresDialect();

var db = new PulseLiteDb(factory, dialect);

var users = await db.Query<User>()
    .FilterSql(x => x.IsActive)
    .ToListAsync();
```

## Basic Usage

### 1. Entity Mapping

PulseORM supports both built-in and custom attributes.

```csharp
using PulseORM.Core;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; }
}
```

### 2. CRUD

```csharp
var user = new User { Email = "john@site.com", IsActive = true };

await db.InsertAsync(user);
await db.UpdateAsync(user);
await db.DeleteByIdAsync<User>(user.Id);

var byId = await db.GetByIdAsync<User>(user.Id);
var all = await db.GetAllAsync<User>();
```

### 3. Pagination

```csharp
var (items, total) = await db.GetAllPagedAsync<User>(
    page: 1,
    pageSize: 20,
    orderBy: x => x.Id,
    descending: false,
    whereInclude: x => x.IsActive
);
```

### 4. Raw SQL + Typed Materialization

```csharp
var admins = await db.SqlQuery<User>(
    "SELECT id, email, is_active FROM users WHERE role = @role",
    new Dictionary<string, object?> { ["role"] = "admin" }
).ToListAsync();
```

### 5. Join Query (Include One)

```csharp
var result = await db.QueryJoin<Order>()
    .IncludeOne<Customer>(o => o.Customer, o => o.CustomerId, c => c.Id, JoinType.Left)
    .FilterSql(o => o.IsActive)
    .SortBy(o => o.Id)
    .Pagination(1, 20)
    .ToListAsync();
```

## Supported Dialects and Factories

- PostgreSQL: `PostgresDialect` + `NpgsqlConnectionFactory`
- SQL Server: `SqlServerDialect` + `SqlConnectionFactory`
- Oracle: `OracleDialect` + `OracleConnectionFactory`

## Build

```bash
dotnet build PulseORM.sln
```

## Notes

- PulseORM is SQL-first by design and does not attempt to hide SQL behavior.
- For production usage, make sure all entity keys and mappings are explicit and verified.

