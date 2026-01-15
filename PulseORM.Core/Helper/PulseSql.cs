using System;

namespace PulseORM.Core.Helper;

public static class PulseSql
{
    public static bool EqualsIgnoreCase(this string left, string right)
        => left.Equals(right, StringComparison.OrdinalIgnoreCase);
}