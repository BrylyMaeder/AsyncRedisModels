using AsyncRedisModels.Attributes;
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
using System.Reflection;
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

        public static async Task<IAsyncModel> LoadAsync(Type modelType, string id)
        {
            if (!typeof(IAsyncModel).IsAssignableFrom(modelType))
            {
                throw new ArgumentException($"Type {modelType.Name} must implement IAsyncModel");
            }

            // Get the static LoadAsync method from RedisRepository that matches the actual signature
            var methodInfo = typeof(RedisRepository).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m =>
                    m.Name == nameof(LoadAsync) &&
                    m.IsGenericMethod &&
                    m.GetParameters().Length == 2 &&  // Changed from 1 to 2 parameters
                    m.GetParameters()[0].ParameterType == typeof(string) &&
                    m.GetParameters()[1].ParameterType.IsArray);  // Looking for the params array

            if (methodInfo == null)
            {
                throw new InvalidOperationException($"No matching LoadAsync method found in RedisRepository for type {modelType.Name}.");
            }

            // Create the generic version with the specified modelType
            var genericMethod = methodInfo.MakeGenericMethod(modelType);

            // Invoke with an empty expressions array since we don't need expressions
            var emptyExpressions = Array.CreateInstance(
                typeof(Expression<>).MakeGenericType(
                    typeof(Func<,>).MakeGenericType(modelType, typeof(object))),
                0);

            var task = (Task)genericMethod.Invoke(null, new object[] { id, emptyExpressions });
            await task.ConfigureAwait(false);

            return (IAsyncModel)task.GetType().GetProperty("Result").GetValue(task);
        }

        public static async Task<TModel> LoadAsync<TModel>(string id, params Expression<Func<TModel, object>>[] expressions)
    where TModel : IAsyncModel
        {
            var db = RedisSingleton.Database;
            var key = ModelHelper.CreateKey<TModel>(id);

            if (!await db.KeyExistsAsync(key))
                return default;

            var memberNames = GetMemberNames(expressions);

            // Check if the HydrateAttribute is applied and enabled on TModel
            var hydrateAttribute = typeof(TModel)
                .GetCustomAttribute<HydrateAttribute>();

            var values = (hydrateAttribute?.Enabled ?? false)
                ? await db.HashGetAsync(key, memberNames) // Fetch values if hydrate is enabled
                : null; // Otherwise, use null values

            var result = ModelFactory.Create<TModel>(id);

            // Always call PopulateModelAsync, passing null if hydrate is not enabled
            await ModelPopulationHelper.PopulateModelAsync(result, memberNames, values);

            return result;
        }

    }
}
