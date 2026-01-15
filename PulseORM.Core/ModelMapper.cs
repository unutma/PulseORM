using System.Reflection;
using System.Collections.Concurrent;

namespace PulseORM.Core;

public sealed class EntityMap
{
    public Type Type { get; init; } = default!;
    public string TableName { get; init; } = default!;
    public PropertyMap? Key { get; init; } = default!;
    public IReadOnlyList<PropertyMap> Properties { get; init; } = default!;
    public IReadOnlyDictionary<string, PropertyMap> PropertyByName { get; init; } = default!;
}

public sealed class PropertyMap
{
    public string ColumnName { get; init; } = default!;
    public PropertyInfo PropertyInfo { get; init; } = default!;
}

public static class ModelMapper
{
    private static readonly ConcurrentDictionary<Type, EntityMap> Cache = new();

    public static EntityMap GetMap<T>() => GetMap(typeof(T));

    public static EntityMap GetMap(Type t)
        => Cache.GetOrAdd(t, BuildMap);

    private static EntityMap BuildMap(Type t)
    {
        var tableAttr = t.GetCustomAttribute<TableAttribute>();
        var tableName = tableAttr?.Name ?? t.Name;

        var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToList();

        var propertyMaps = props
            .Select(p =>
            {
                var colAttr = p.GetCustomAttribute<ColumnAttribute>();
                var colName = colAttr?.Name ?? p.Name;
                return new PropertyMap
                {
                    ColumnName = colName,
                    PropertyInfo = p
                };
            })
            .ToList();

        var propertyDict = propertyMaps
            .ToDictionary(p => p.PropertyInfo.Name, p => p);

        var keyProp = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);

        PropertyMap? keyMap = null;
        if (keyProp is not null)
        {
            keyMap = new PropertyMap
            {
                ColumnName = keyProp.GetCustomAttribute<ColumnAttribute>()?.Name ?? keyProp.Name,
                PropertyInfo = keyProp
            };
        }

        return new EntityMap
        {
            Type = t,
            TableName = tableName,
            Key = keyMap,
            Properties = propertyMaps,
            PropertyByName = propertyDict
        };
    }
}
