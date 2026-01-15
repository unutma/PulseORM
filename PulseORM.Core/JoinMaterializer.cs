using System;
using System.Collections.Generic;
using System.Data;

namespace PulseORM.Core;

internal static class JoinMaterializer
{
    public static List<TRoot> MaterializeOneToOne<TRoot>(IDataReader reader, QueryPlan plan)
        where TRoot : new()
    {
        var rootFactory = PrefixFactory.Create<TRoot>(plan.RootMap, plan.RootPrefix, reader);

        var joinFactories = new List<(IJoinSpec Spec, Func<object?> Factory)>();
        foreach (var (jm, spec) in plan.Joins)
            joinFactories.Add((spec, PrefixFactory.CreateUntyped(jm, spec.Prefix, reader)));

        var rootKeyGetter = KeyGetter.Create<TRoot>(plan.RootMap);

        var dict = new Dictionary<object, TRoot>();

        while (reader.Read())
        {
            var root = rootFactory();
            var rootKey = rootKeyGetter(root);

            if (!dict.TryGetValue(rootKey, out var tracked))
            {
                tracked = root;
                dict[rootKey] = tracked;
            }

            foreach (var (spec, fac) in joinFactories)
            {
                var joined = fac();
                spec.ApplyOne(tracked, joined);
            }
        }

        return new List<TRoot>(dict.Values);
    }

    public static List<TRoot> MaterializeOneToMany<TRoot>(IDataReader reader, QueryPlan plan)
        where TRoot : new()
    {
        var rootFactory = PrefixFactory.Create<TRoot>(plan.RootMap, plan.RootPrefix, reader);

        var joinFactories = new List<(EntityMap Map, IJoinSpec Spec, Func<object?> Factory, Func<object, object>? KeyGet)>();
        foreach (var (jm, spec) in plan.Joins)
        {
            Func<object, object>? keyGet = null;
            if (jm.Key is not null)
                keyGet = KeyGetter.CreateUntyped(jm);
            joinFactories.Add((jm, spec, PrefixFactory.CreateUntyped(jm, spec.Prefix, reader), keyGet));
        }

        var rootKeyGetter = KeyGetter.Create<TRoot>(plan.RootMap);

        var dict = new Dictionary<object, TRoot>();
        var childSeen = new Dictionary<(object RootKey, int JoinIndex), HashSet<object>>();

        var joinIndex = 0;

        while (reader.Read())
        {
            var root = rootFactory();
            var rootKey = rootKeyGetter(root);

            if (!dict.TryGetValue(rootKey, out var tracked))
            {
                tracked = root;
                dict[rootKey] = tracked;
            }

            for (int i = 0; i < joinFactories.Count; i++)
            {
                var (jm, spec, fac, keyGet) = joinFactories[i];
                var joined = fac();
                if (joined is null) continue;

                if (!spec.IsMany)
                {
                    spec.ApplyOne(tracked, joined);
                    continue;
                }

                if (keyGet is null)
                {
                    spec.ApplyMany(tracked, joined);
                    continue;
                }

                var childKey = keyGet(joined);
                var k = (rootKey, i);

                if (!childSeen.TryGetValue(k, out var hs))
                {
                    hs = new HashSet<object>();
                    childSeen[k] = hs;
                }

                if (hs.Add(childKey))
                    spec.ApplyMany(tracked, joined);
            }

            joinIndex++;
        }

        return new List<TRoot>(dict.Values);
    }
}

internal static class KeyGetter
{
    public static Func<T, object> Create<T>(EntityMap map)
    {
        var keyProp = map.Key?.PropertyInfo
            ?? throw new InvalidOperationException($"No key mapped for {typeof(T).Name}.");

        return x => keyProp.GetValue(x)!;
    }

    public static Func<object, object> CreateUntyped(EntityMap map)
    {
        var keyProp = map.Key?.PropertyInfo
            ?? throw new InvalidOperationException($"No key mapped for {map.TableName}.");

        return x => keyProp.GetValue(x)!;
    }
}

internal static class PrefixFactory
{
    public static Func<T> Create<T>(EntityMap map, string prefix, IDataReader reader) where T : new()
    {
        var ord = BuildOrdinals(map, prefix, reader);
        return () => CreateInstance<T>(map, ord, reader);
    }

    public static Func<object?> CreateUntyped(EntityMap map, string prefix, IDataReader reader)
    {
        var ord = BuildOrdinals(map, prefix, reader);
        return () => CreateInstanceUntyped(map, ord, reader);
    }

    private static Dictionary<string, int> BuildOrdinals(EntityMap map, string prefix, IDataReader reader)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
            dict[reader.GetName(i)] = i;

        var ord = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in map.Properties)
        {
            var name = prefix + p.ColumnName;
            if (dict.TryGetValue(name, out var idx))
                ord[p.ColumnName] = idx;
        }
        return ord;
    }

    private static T CreateInstance<T>(EntityMap map, Dictionary<string, int> ord, IDataReader reader) where T : new()
    {
        var obj = new T();

        foreach (var p in map.Properties)
        {
            if (!ord.TryGetValue(p.ColumnName, out var idx))
                continue;

            var val = reader.GetValue(idx);
            if (val is DBNull) continue;

            p.PropertyInfo.SetValue(obj, Convert.ChangeType(val, Nullable.GetUnderlyingType(p.PropertyInfo.PropertyType) ?? p.PropertyInfo.PropertyType));
        }

        return obj;
    }

    private static object? CreateInstanceUntyped(EntityMap map, Dictionary<string, int> ord, IDataReader reader)
    {
        var obj = Activator.CreateInstance(map.Type);
        var allNull = true;

        foreach (var p in map.Properties)
        {
            if (!ord.TryGetValue(p.ColumnName, out var idx))
                continue;

            var val = reader.GetValue(idx);
            if (val is DBNull) continue;

            allNull = false;
            p.PropertyInfo.SetValue(obj, Convert.ChangeType(val, Nullable.GetUnderlyingType(p.PropertyInfo.PropertyType) ?? p.PropertyInfo.PropertyType));
        }

        return allNull ? null : obj;
    }
}
