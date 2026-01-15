using System;

namespace PulseORM.Core;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class TableAttribute : Attribute
{
    public string Name { get; }
    public TableAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ColumnAttribute : Attribute
{
    public string Name { get; }
    public ColumnAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class KeyAttribute : Attribute
{
}