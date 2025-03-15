using Newtonsoft.Json;
using StackExchange.Redis;
using System;

namespace AsyncRedisModels.Internal
{
    internal static class RedisValueExtensions
    {
        internal static RedisValue ConvertToRedisValue<T>(this object value)
        {
            if (value == null)
                return RedisValue.Null;

            if (typeof(T).IsEnum)
                return value.ToString();

            if (typeof(T) == typeof(DateTime))
            {
                var dateTime = (DateTime)value;
                var unixTimestamp = new DateTimeOffset(dateTime).ToUnixTimeSeconds();
                return unixTimestamp.ToString();
            }

            if (typeof(T) == typeof(TimeSpan))
            {
                var timeSpan = (TimeSpan)value;
                return timeSpan.TotalSeconds.ToString(); // Store TimeSpan as total seconds
            }

            if (typeof(T) == typeof(bool))
                return (bool)value ? "1" : "0";

            if (typeof(T) == typeof(int))
                return ((int)value).ToString();

            if (typeof(T) == typeof(long))
                return ((long)value).ToString();

            if (typeof(T) == typeof(double))
                return ((double)value).ToString();

            if (typeof(T) == typeof(decimal))
                return ((decimal)value).ToString();

            if (typeof(T).IsClass && typeof(T) != typeof(string))
            {
                var json = JsonConvert.SerializeObject(value, JsonSettings);
                return json;
            }

            return value?.ToString() ?? string.Empty;
        }


        public static T ConvertFromRedisValue<T>(this RedisValue value)
        {
            if (value.IsNull)
                return default;

            if (typeof(T).IsEnum)
                return (T)Enum.Parse(typeof(T), value);

            if (typeof(T) == typeof(DateTime))
            {
                var unixTimestamp = long.Parse(value);
                var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
                return (T)(object)dateTime;
            }

            if (typeof(T) == typeof(TimeSpan))
            {
                var totalSeconds = double.Parse(value);
                return (T)(object)TimeSpan.FromSeconds(totalSeconds); // Convert from total seconds
            }

            if (typeof(T) == typeof(bool))
                return (T)(object)(value == "1");

            if (typeof(T) == typeof(int))
                return (T)(object)int.Parse(value);

            if (typeof(T) == typeof(long))
                return (T)(object)long.Parse(value);

            if (typeof(T) == typeof(double))
                return (T)(object)double.Parse(value);

            if (typeof(T) == typeof(decimal))
                return (T)(object)decimal.Parse(value);

            if (typeof(T) == typeof(string))
                return (T)(object)value.ToString();

            if (typeof(T).IsClass)
                return JsonConvert.DeserializeObject<T>(value, JsonSettings);

            try
            {
                return JsonConvert.DeserializeObject<T>(value, JsonSettings);
            }
            catch
            {
                return default;
            }
        }


        // JSON serializer settings to avoid circular references or null values
        static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };
    }
}
