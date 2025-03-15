using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncRedisModels.Contracts;
using AsyncRedisModels.Extensions;
using System.Reflection;

namespace AsyncRedisModels
{
    public static class IAsyncModelExtensions
    {
        public static string GetKey(this IAsyncModel document)
        {
            return $"{document.IndexName()}:{document.Id}";
        }

        public static async Task DeleteAsync(this IAsyncModel model)
        {
            try
            {
                await RedisSingleton.Database.KeyDeleteAsync(model.GetKey());

            }
            catch (Exception ex)
            {
                // Log or handle errors here
                Console.WriteLine($"Error during deletion: {ex.Message}");
            }
        }

        public static async Task PushAsync<T>(this T entity, params Expression<Func<T, object>>[] expressions) where T : IAsyncModel
        {
            var db = RedisSingleton.Database;
            var memberNames = expressions
                .Select(exp => MemberSelector.GetMemberName(exp))
                .Select(name => (RedisValue)name)
                .ToArray();

            var values = new RedisValue[memberNames.Length];

            // Get the values from the properties and map them
            for (int i = 0; i < memberNames.Length; i++)
            {
                var memberName = memberNames[i];
                var property = typeof(T).GetProperty(memberName);

                if (property != null && property.CanRead)
                {
                    var value = property.GetValue(entity);
                    values[i] = value != null ? value.SerializeToRedis() : RedisValue.Null;
                }
            }

            await db.HashSetAsync(entity.GetKey(), memberNames.Zip(values, (name, value) => new HashEntry(name, value)).ToArray());
        }

        public static async Task PullAsync<T>(this T entity, params Expression<Func<T, object>>[] expressions) where T : IAsyncModel
        {
            var db = RedisSingleton.Database;
            var memberNames = expressions
                .Select(exp => MemberSelector.GetMemberName(exp))
                .Select(name => (RedisValue)name)
                .ToArray();

            // Retrieve the values from Redis
            var values = await db.HashGetAsync(entity.GetKey(), memberNames);

            // Set the values to the properties of the entity
            for (int i = 0; i < memberNames.Length; i++)
            {
                var memberName = memberNames[i];
                var value = values[i];

                if (value.HasValue)
                {
                    var property = typeof(T).GetProperty(memberName);

                    if (property != null && property.CanWrite)
                    {
                        // Convert and set the value to the property
                        var convertedValue = value.DeserializeFromRedis(property.PropertyType);
                        property.SetValue(entity, convertedValue);
                    }
                }
            }
        }

        public static async Task<long> IncrementAsync<T>(this T entity, Expression<Func<T, long>> expression) where T : IAsyncModel
        {
            var db = RedisSingleton.Database;

            if (!(expression.Body is MemberExpression memberExpr))
                throw new ArgumentException("Invalid expression.");

            var property = memberExpr.Member as PropertyInfo;
            if (property == null)
                throw new ArgumentException("Expression must be a property.");

            var key = entity.GetKey();
            long newValue = await db.StringIncrementAsync(key);

            // Update entity property
            property.SetValue(entity, newValue);

            return newValue;
        }


    }
}
