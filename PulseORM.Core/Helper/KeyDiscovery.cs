namespace PulseORM.Core.Helper;

using System.Reflection;

internal static class KeyDiscovery
{
    internal static PropertyInfo? FindKeyProperty(Type t)
    {
        var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);

        var byAttr = props.FirstOrDefault(HasKeyAttribute);
        if (byAttr != null) return byAttr;

        var id = props.FirstOrDefault(p =>
            string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase));
        if (id != null) return id;

        var typeIdName = t.Name + "Id";
        return props.FirstOrDefault(p =>
            string.Equals(p.Name, typeIdName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasKeyAttribute(PropertyInfo p)
    {
        return p.GetCustomAttributes(true)
            .Any(a =>
            {
                var t = a.GetType();
                return t.Name == "KeyAttribute"
                       || t.FullName == "System.ComponentModel.DataAnnotations.KeyAttribute"
                       || t.FullName == "PulseORM.Core.KeyAttribute";
            });
    }
}
