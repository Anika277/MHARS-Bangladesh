using System.Reflection;

namespace MHARS.Web.Helpers;

public static class ObjectDisplayHelper
{
    // Only pull "simple" properties (skip navigation properties/collections to avoid clutter or lazy-load issues)
    public static List<PropertyInfo> GetSimpleProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsPrimitive
                        || p.PropertyType == typeof(string)
                        || p.PropertyType == typeof(decimal)
                        || p.PropertyType == typeof(DateTime)
                        || p.PropertyType == typeof(DateTime?)
                        || p.PropertyType == typeof(Guid)
                        || p.PropertyType.IsEnum)
            .ToList();
    }

    public static string GetValue(object item, PropertyInfo prop)
    {
        var value = prop.GetValue(item);
        if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
        return value?.ToString() ?? "-";
    }
}