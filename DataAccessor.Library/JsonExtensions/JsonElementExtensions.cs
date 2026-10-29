using System;
using System.Text.Json;

namespace DataAccessor.Library.JsonExtensions
{
    public static class JsonElementExtensions
    {
        public static string? GetStringOrNull(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        public static int? GetInt32OrNull(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                return value;
            return null;
        }

        public static double? GetDoubleOrNull(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var value))
                return value;
            return null;
        }

        public static bool? GetBooleanOrNull(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True)
                return true;
            if (element.TryGetProperty(propertyName, out var prop2) && prop2.ValueKind == JsonValueKind.False)
                return false;
            return null;
        }

        public static DateTime? GetDateTimeOrNull(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String && DateTime.TryParse(prop.GetString(), out var dt))
                return dt;
            return null;
        }
    }
}
