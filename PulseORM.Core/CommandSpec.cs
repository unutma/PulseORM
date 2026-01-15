namespace PulseORM.Core;

public sealed record CommandSpec(
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters
);
