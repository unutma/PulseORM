namespace PulseORM.Core.Sql;

internal static class LikeUtil
{
    public const char EscapeChar = '\\';

    public static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // önce escape char'ı kaçır
        s = s.Replace(@"\", @"\\");
        // sonra LIKE wildcard karakterlerini kaçır
        s = s.Replace("%",  @"\%");
        s = s.Replace("_",  @"\_");
        return s;
    }

    public static string Contains(string term)   => $"%{Escape(term)}%";
    public static string StartsWith(string term) => $"{Escape(term)}%";
    public static string EndsWith(string term)   => $"%{Escape(term)}";
}