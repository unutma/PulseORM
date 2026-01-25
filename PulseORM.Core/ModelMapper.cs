using System.Reflection;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using PulseORM.Core.Helper;

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
            .Where(p => p.GetCustomAttribute<NotMappedAttribute>() is null)
            .Where(p => IsDbScalar(p.PropertyType)) 
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
            .ToDictionary(p => p.PropertyInfo.Name, p => p, StringComparer.OrdinalIgnoreCase);


        var keyProp = KeyDiscovery.FindKeyProperty(t);

        PropertyMap? keyMap = null;
        if (keyProp is not null)
        {
            var keyColAttr = keyProp.GetCustomAttribute<ColumnAttribute>();
            var keyColName = keyColAttr?.Name ?? keyProp.Name;

            keyMap = new PropertyMap
            {
                ColumnName = keyColName,
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
    
    private static bool IsDbScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsEnum) return true;
        if (type.IsPrimitive) return true;

        return type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(DateOnly)
               || type == typeof(TimeOnly)
               || type == typeof(Guid)
               || type == typeof(byte[])
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan);
    }
}
