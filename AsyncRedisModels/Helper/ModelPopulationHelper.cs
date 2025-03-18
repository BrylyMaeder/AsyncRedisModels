using AsyncRedisModels.Attributes;
using AsyncRedisModels.Contracts;
using AsyncRedisModels.Extensions;
using AsyncRedisModels.Factory;
using AsyncRedisModels.Repository;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AsyncRedisModels.Helper
{
    public static class ModelPopulationHelper
    {
        public static async Task PopulateModelAsync<TModel>(TModel model, RedisValue[] memberNames, RedisValue[] values)
            where TModel : IAsyncModel
        {
            var loadingTasks = new List<Task>();
            var loadedInstances = new Dictionary<string, IAsyncModel>();

            // Process all properties from Redis data
            for (int i = 0; i < memberNames.Length; i++)
            {
                string memberName = memberNames[i].ToString();


                if (memberName == "Id")
                    continue;

                PropertyInfo property = typeof(TModel).GetProperty(
                    memberName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );

                if (property == null || !property.CanWrite)
                    continue;

                Type propertyType = property.PropertyType;

                // Skip IModelComponent properties
                if (typeof(IModelComponent).IsAssignableFrom(propertyType))
                    continue;

                // Handle IAsyncModel properties
                if (typeof(IAsyncModel).IsAssignableFrom(propertyType))
                {
                    string id = string.Format("{0}_{1}", model.Id, memberName);
                    await ProcessAsyncModelProperty(
                        model,
                        property,
                        propertyType,
                        id,
                        loadingTasks,
                        loadedInstances
                    );
                    continue;
                }

                if (values == null)
                    continue;

                RedisValue value = values[i];

                // Handle simple value properties
                object convertedValue = value.DeserializeFromRedis(propertyType);
                property.SetValue(model, convertedValue);
            }

            // Wait for all async operations and assign loaded instances
            if (loadingTasks.Count > 0)
            {
                await Task.WhenAll(loadingTasks);
                AssignLoadedInstances(model, memberNames, loadedInstances);
            }
        }

        private static async Task ProcessAsyncModelProperty<TModel>(
            TModel model,
            PropertyInfo property,
            Type propertyType,
            string id,
            List<Task> loadingTasks,
            Dictionary<string, IAsyncModel> loadedInstances)
            where TModel : IAsyncModel
        {
            HydrateAttribute loadAttr = Attribute.GetCustomAttribute(property, typeof(HydrateAttribute)) as HydrateAttribute;
            bool shouldLoad = loadAttr != null && loadAttr.Enabled;

            if (shouldLoad)
            {
                loadingTasks.Add(Task.Run(async () =>
                {
                    IAsyncModel instance = await RedisRepository.LoadAsync(propertyType, id);
                    if (instance == null)
                    {
                        instance = CreateNewInstance(propertyType, id);
                    }
                    loadedInstances[id] = instance;
                }));
            }
            else
            {
                IAsyncModel instance = ModelFactory.CreateEmpty(propertyType);
                instance.Id = id;
                property.SetValue(model, instance);
            }
        }

        private static IAsyncModel CreateNewInstance(Type propertyType, string id)
        {
            IAsyncModel instance = ModelFactory.CreateEmpty(propertyType, id);
            instance.CreatedAt = DateTime.UtcNow;
            Task pushTask = instance.PushAsync(s => s.CreatedAt);
            // Fire and forget as in original
            return instance;
        }

        private static void AssignLoadedInstances<TModel>(
            TModel model,
            RedisValue[] memberNames,
            Dictionary<string, IAsyncModel> loadedInstances)
            where TModel : IAsyncModel
        {
            foreach (RedisValue memberName in memberNames)
            {
                PropertyInfo property = typeof(TModel).GetProperty(
                    memberName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );

                if (property != null &&
                    typeof(IAsyncModel).IsAssignableFrom(property.PropertyType))
                {
                    string id = string.Format("{0}_{1}", model.Id, memberName);
                    IAsyncModel instance;
                    if (loadedInstances.TryGetValue(id, out instance))
                    {
                        property.SetValue(model, instance);
                    }
                }
            }
        }
    }
}
