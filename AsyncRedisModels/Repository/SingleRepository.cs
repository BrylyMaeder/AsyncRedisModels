using AsyncRedisModels.Contracts;
using AsyncRedisModels.Extensions;
using AsyncRedisModels.Factory;
using AsyncRedisModels.Helper;
using AsyncRedisModels.Index.Models;
using AsyncRedisModels.Models;
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
        public static async Task<ModelCreationResult<TModel>> CreateAsync<TModel>(TModel newModel) where TModel : IAsyncModel
        {
            var index = ModelHelper.GetIndex<TModel>();
            var db = RedisSingleton.Database;

            try
            {
                newModel.CreatedAt = DateTime.UtcNow;

                // Ensure uniqueness atomically
                var modelKey = newModel.GetKey();
                if (await db.KeyExistsAsync(modelKey))
                {
                    return new ModelCreationResult<TModel>
                    {
                        Data = newModel,
                        Message = $"An object with the key [{newModel.GetKey()}] already exists.",
                        Succeeded = true,
                    };
                }

                // Save the new model
                await newModel.PushAsync(s => s.CreatedAt);

                return new ModelCreationResult<TModel>
                {
                    Data = newModel,
                    Message = "Successfully created",
                    Succeeded = true,
                };
            }
            catch (Exception ex)
            {
                // Handle exceptions gracefully
                // Log the exception (optional)
                throw new InvalidOperationException($"Failed to create the model with key {index}:{newModel.Id}.", ex);
            }
        }

        public static async Task<ModelCreationResult<TModel>> CreateAsync<TModel>(string id = "") where TModel : IAsyncModel
        {
            var index = ModelHelper.GetIndex<TModel>();
            var db = RedisSingleton.Database;

            // Validate ID
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString();
            }

            try
            {
                var newModel = ModelFactory.Create<TModel>(id);
                newModel.CreatedAt = DateTime.UtcNow;

                // Ensure uniqueness atomically
                var modelKey = newModel.GetKey();
                if (await db.KeyExistsAsync(modelKey))
                {
                    return new ModelCreationResult<TModel>
                    {
                        Data = newModel,
                        Message = $"An object with the key [{newModel.GetKey()}] already exists.",
                        Succeeded = false,
                    };
                }

                // Save the new model
                await newModel.PushAsync(s => s.CreatedAt);

                return new ModelCreationResult<TModel>
                {
                    Data = newModel,
                    Message = "Successfully created",
                    Succeeded = true,
                };
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
                return default;

            var memberNames = expressions.Any()
                ? expressions.Select(exp => MemberSelector.GetMemberName(exp)).Select(name => (RedisValue)name).ToArray()
                : typeof(TModel).GetProperties().Select(p => (RedisValue)p.Name).ToArray();

            var values = await db.HashGetAsync(key, memberNames);
            var result = ModelFactory.Create<TModel>(id);

            for (int i = 0; i < memberNames.Length; i++)
            {
                var memberName = memberNames[i];
                var value = values[i];

                if (value.HasValue)
                {
                    var property = typeof(TModel).GetProperty(memberName);

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
                        var convertedValue = value.DeserializeFromRedis(property.PropertyType);
                        property.SetValue(result, convertedValue);
                    }
                }
            }

            return result;
        }
    }
}
