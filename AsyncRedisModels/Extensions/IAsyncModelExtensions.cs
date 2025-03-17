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
using AsyncRedisModels.Repository;
using AsyncRedisModels.Attributes;
using AsyncRedisModels.Models;
using AsyncRedisDocuments;
using AsyncRedisModels.Query;
using AsyncRedisModels.Helper;
using System.Xml.Linq;

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
            await RedisSingleton.Database.KeyDeleteAsync(model.GetKey());

            if (model is IDeletionListener listener)
            {
                //inform a document listener that it's document is deleted.
                await listener.OnDeleted();
            }

            var properties = model.GetType().GetProperties();

            // Iterate all components and delete them
            foreach (var property in properties)
            {
                // Check if the property is readable and of a type that implements IDeletable
                if (property.CanRead)
                {
                    var value = property.GetValue(model);
                    if (value is IDeletable deletable)
                    {
                        // Call DeleteAsync on the IDeletable instance
                        await deletable.DeleteAsync();
                    }
                }
            }

            const int batchSize = 100; //Cleanup dead keys
            var cursor = 0L;

            try
            {
                do
                {
                    // Execute SCAN command to find keys with the specified pattern
                    var scanResult = await RedisSingleton.Database.ExecuteAsync("SCAN", cursor.ToString(), "MATCH", $"{model.GetKey()}*", "COUNT", batchSize);

                    // Parse the SCAN result
                    var resultArray = (RedisResult[])scanResult;
                    cursor = long.Parse(resultArray[0].ToString()); // Update cursor for next iteration
                    var keys = ((RedisResult[])resultArray[1]).Select(r => (RedisKey)r).ToArray(); // Collect keys

                    if (keys.Any())
                    {
                        // Batch delete keys asynchronously
                        await RedisSingleton.Database.KeyDeleteAsync(keys);
                    }
                } while (cursor != 0); // Continue until cursor is 0

            }
            catch (Exception ex)
            {
                // Log or handle errors here
                Console.WriteLine($"Error during deletion: {ex.Message}");
            }
        }

        public static async Task<ModelPushResult<T>> PushAsync<T>(this T entity, Expression<Func<T, object>> expression) where T : IAsyncModel
        {
            // Reuse the logic from the enumerable method by calling it with a single expression in an array
            var results = await PushAsync(entity, new Expression<Func<T, object>>[] { expression });

            // Return the first result since we only passed one expression
            return results.FirstOrDefault();
        }


        public static async Task<IEnumerable<ModelPushResult<T>>> PushAsync<T>(this T entity, params Expression<Func<T, object>>[] expressions) where T : IAsyncModel
        {
            var memberNames = expressions.Select(exp => MemberSelector.GetMemberName(exp)).ToArray();
            var results = new List<ModelPushResult<T>>();

            foreach (var memberName in memberNames)
            {
                var result = await PushPropertyAsync(entity, memberName);
                results.Add(result);
            }

            return results;
        }

        public static async Task<IEnumerable<ModelPushResult<T>>> PushAsync<T>(this T entity, params string[] propertyNames) where T : IAsyncModel
        {
            var results = new List<ModelPushResult<T>>();

            foreach (var propertyName in propertyNames)
            {
                var result = await PushPropertyAsync(entity, propertyName);
                results.Add(result);
            }

            return results;
        }

        private static async Task<ModelPushResult<T>> PushPropertyAsync<T>(T entity, string propertyName) where T : IAsyncModel
        {
            var db = RedisSingleton.Database;
            var property = typeof(T).GetProperty(propertyName);
            var result = new ModelPushResult<T> { Data = entity, PropertyName = propertyName, Succeeded = true };

            if (property == null || !property.CanRead)
            {
                result.Succeeded = false;
                result.Message = $"Property '{propertyName}' not found or cannot be read.";
                return result;
            }

            // Skip properties implementing IModelComponent
            if (typeof(IModelComponent).IsAssignableFrom(property.PropertyType) || typeof(IAsyncModel).IsAssignableFrom(property.PropertyType))
            {
                result.Succeeded = false;
                result.Message = $"Property '{propertyName}' has an unsupported type and is skipped.";
                return result;
            }


            var value = property.GetValue(entity);
            var redisValue = value != null ? value.SerializeToRedis() : RedisValue.Null;

            // Check for Unique attribute
            var uniqueAttribute = property.GetCustomAttribute<UniqueAttribute>();
            if (uniqueAttribute != null)
            {
                var uniqueCondition = QueryHelper.Tag(propertyName, value);
                var indexName = ModelHelper.GetIndex<T>();

                var redisQuery = new RedisQuery(indexName);
                redisQuery.Conditions.Add(uniqueCondition);

                var exists = await redisQuery.AnyAsync();

                if (exists)
                {
                    result.Succeeded = false;
                    result.Message = $"Unique Property '[{propertyName}: {value}]' is not available.";
                    return result;
                }
            }

            // Push to Redis
            await db.HashSetAsync(entity.GetKey(), new[] { new HashEntry(propertyName, redisValue) });
            result.Message = "Push successful.";
            return result;
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
                        // Skip properties implementing IModelComponent
                        if (typeof(IModelComponent).IsAssignableFrom(property.PropertyType))
                        {
                            continue;
                        }
                        if (typeof(IAsyncModel).IsAssignableFrom(property.PropertyType))
                        {
                            continue;
                        }


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
