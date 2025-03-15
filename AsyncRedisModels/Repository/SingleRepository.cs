using AsyncRedisModels.Contracts;
using AsyncRedisModels.Extensions;
using AsyncRedisModels.Factory;
using AsyncRedisModels.Helper;
using AsyncRedisModels.Index.Models;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AsyncRedisModels.Repository
{
    public partial class RedisRepository
    {
        public static async Task<TModel> CreateAsync<TModel>(string id = "") where TModel : IAsyncModel
        {
            var index = ModelHelper.GetIndex<TModel>();
            var db = RedisSingleton.Database;

            // Validate ID
            if (string.IsNullOrWhiteSpace(id))
            {
                id = $"{await db.HashIncrementAsync($"index:counters", index)}";
            }

            try
            {
                var newModel = ModelFactory.Create<TModel>(id);
                newModel.CreatedAt = DateTime.UtcNow;

                // Ensure uniqueness atomically
                var modelKey = newModel.GetKey();
                if (await db.KeyExistsAsync(modelKey))
                {
                    throw new Exception("An object with that ID already exists.");
                }

                // Save the new model
                await newModel.PushAsync(s => s.CreatedAt);

                return newModel;
            }
            catch (Exception ex)
            {
                // Handle exceptions gracefully
                // Log the exception (optional)
                throw new InvalidOperationException($"Failed to create the model with key {index}:{id}.", ex);
            }
        }

        public static async Task<TModel> LoadAsync<TModel>(string id, params Expression<Func<TModel, object>>[] expressions) where TModel : IAsyncModel
        {
            var db = RedisSingleton.Database;

            var key = ModelHelper.CreateKey<TModel>(id);

            if (!await db.KeyExistsAsync(key))
            {
                return default;
            }

            // Get the member names from expressions
            var memberNames = expressions
                .Select(exp => MemberSelector.GetMemberName(exp))
                .Select(name => (RedisValue)name)
                .ToArray();

            // Retrieve the values for the specific ID
            var values = await db.HashGetAsync(key, memberNames);

            // Create an instance of T to hold the result
            var result = ModelFactory.Create<TModel>(id);

            // Map values to corresponding properties
            for (int i = 0; i < memberNames.Length; i++)
            {
                var memberName = memberNames[i];
                var value = values[i];

                if (value.HasValue)
                {
                    var property = typeof(TModel).GetProperty(memberName);

                    if (property != null && property.CanWrite)
                    {
                        // Convert and set the property value
                        var convertedValue = value.DeserializeFromRedis(property.PropertyType);
                        property.SetValue(result, convertedValue);
                    }
                }
            }

            return result;
        }

    }
}
