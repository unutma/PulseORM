using System;
using System.Data;
using System.Globalization;

namespace PulseORM.Core;

public static class Materializer
{
    public static List<T> Materialize<T>(IDataReader reader) where T : new()
    {
        var map = ModelMapper.GetMap<T>();

        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
            columnIndex[reader.GetName(i)] = i;

        var setters = map.Properties
            .Where(p => columnIndex.ContainsKey(p.ColumnName))
            .Select(p => new
            {
                p.PropertyInfo,
                Index = columnIndex[p.ColumnName]
            })
            .ToList();

        var list = new List<T>();

        while (reader.Read())
        {
            var entity = new T();

            foreach (var s in setters)
            {
                if (reader.IsDBNull(s.Index))
                    continue;

                var raw = reader.GetValue(s.Index);
                var converted = ConvertTo(raw, s.PropertyInfo.PropertyType);
                s.PropertyInfo.SetValue(entity, converted);
            }

            list.Add(entity);
        }

        return list;
    }

    private static object? ConvertTo(object raw, Type targetType)
    {
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (raw is null)
            return null;

        if (t.IsInstanceOfType(raw))
            return raw;

        if (t == typeof(string))
            return raw.ToString();

        if (t == typeof(Guid))
        {
            if (raw is Guid g) return g;
            return Guid.Parse(raw.ToString()!);
        }

        if (t == typeof(bool))
        {
            if (raw is bool b) return b;
            if (raw is int i) return i != 0;
            if (raw is long l) return l != 0;
            if (raw is short sh) return sh != 0;
            if (raw is byte by) return by != 0;
            return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
        }

        if (t.IsEnum)
        {
            if (raw is string s) return Enum.Parse(t, s, ignoreCase: true);
            return Enum.ToObject(t, raw);
        }

        return Convert.ChangeType(raw, t, CultureInfo.InvariantCulture);
    }
}
