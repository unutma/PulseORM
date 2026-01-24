namespace PulseORM.Core.Sql;

internal static class LikeUtil
{
    public const char EscapeChar = '\\';

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        
        s = s.Replace(@"\", @"\\");
        s = s.Replace("%",  @"\%");
        s = s.Replace("_",  @"\_");
        return s;
    }

    internal static string Contains(string term)   => $"%{Escape(term)}%";
    internal static string StartsWith(string term) => $"{Escape(term)}%";
    internal static string EndsWith(string term)   => $"%{Escape(term)}";
}