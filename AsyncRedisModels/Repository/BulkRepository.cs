using AsyncRedisModels.Contracts;
using AsyncRedisModels.Factory;
using AsyncRedisModels.Helper;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using AsyncRedisModels.Extensions;
using System.Linq.Expressions;

namespace AsyncRedisModels.Repository
{
    public partial class RedisRepository
    {
        #region Creation
        public static async Task<IEnumerable<TModel>> CreateManyAsync<TModel>(int amount, CancellationToken cancellationToken = default)
    where TModel : IAsyncModel
        {
            var index = ModelHelper.GetIndex<TModel>();
            var db = RedisSingleton.Database;
            long newIds = await db.HashIncrementAsync($"index:counters", index, amount, flags: CommandFlags.None);
            long startId = newIds - amount;
            var models = new List<TModel>();

            // Create batch to check key existence
            var batchCreate = db.CreateBatch();
            var tasksCreate = new List<Task<bool>>();

            try
            {
                for (long i = startId; i < startId + amount; i++)
                {
                    var id = $"{i}";
                    var newModel = ModelFactory.Create<TModel>(id);
                    newModel.CreatedAt = DateTime.UtcNow;

                    tasksCreate.Add(batchCreate.KeyExistsAsync(newModel.GetKey()));
                    models.Add(newModel);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create the models starting from {index}:{startId}.", ex);
            }

            batchCreate.Execute();
            if ((await Task.WhenAll(tasksCreate)).Any(s => !s))
                throw new Exception("Failed to create object(s). Some object IDs already existed.");

            // Persist model data atomically
            await PersistModels(models, db, cancellationToken);

            return models;
        }

        public static async Task<IEnumerable<TModel>> CreateManyAsync<TModel>(IEnumerable<string> ids, CancellationToken cancellationToken = default)
            where TModel : IAsyncModel
        {
            var index = ModelHelper.GetIndex<TModel>();
            var db = RedisSingleton.Database;

            // Validate IDs
            if (ids == null || !ids.Any() || ids.Any(string.IsNullOrEmpty))
                throw new ArgumentException("Invalid input IDs.", nameof(ids));

            var models = new List<TModel>();

            // Batch check for existing IDs
            var batch = db.CreateBatch();
            var tasks = ids.Select(id => batch.KeyExistsAsync($"{index}:{id}")).ToArray();
            batch.Execute();

            if ((await Task.WhenAll(tasks)).Any(exists => exists))
                throw new Exception("One or more provided IDs already exist.");

            // Create models and check uniqueness
            var batchCreate = db.CreateBatch();
            var tasksCreate = new List<Task<bool>>();

            foreach (var id in ids)
            {
                try
                {
                    var newModel = ModelFactory.Create<TModel>(id);
                    newModel.CreatedAt = DateTime.UtcNow;

                    tasksCreate.Add(batchCreate.KeyExistsAsync(newModel.GetKey()));
                    models.Add(newModel);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to create the model with key {index}:{id}.", ex);
                }
            }

            batchCreate.Execute();
            if ((await Task.WhenAll(tasksCreate)).Any(s => !s))
                throw new Exception("Failed to create objects. One or more object IDs already existed.");

            // Persist models atomically
            await PersistModels(models, db, cancellationToken);

            return models;
        }

        private static async Task PersistModels<TModel>(List<TModel> models, IDatabase db, CancellationToken cancellationToken) where TModel : IAsyncModel
        {
            var transaction = db.CreateTransaction();
            var tasks = models.Select(model => transaction.HashSetAsync(
                model.GetKey(),
                "CreatedAt",
                model.CreatedAt.SerializeToRedis()
            ));

            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new Exception("Failed to commit model creation transaction.");

            await Task.WhenAll(tasks);
        }

        #endregion

        #region Loading
        public static async Task<IEnumerable<TModel>> LoadManyAsync<TModel>(IEnumerable<string> ids, params Expression<Func<TModel, object>>[] expressions) where TModel : IAsyncModel
        {
            var db = RedisSingleton.Database;
            var indexName = ModelHelper.GetIndex<TModel>();

            var batch = db.CreateBatch();
            var keys = new List<string>();
            var tasks = new List<Task<bool>>();

            // Check if keys exist
            foreach (var id in ids)
            {
                var key = $"{indexName}:{id}";
                tasks.Add(batch.KeyExistsAsync(key));  // Check if the key exists
                keys.Add(key);
            }

            batch.Execute();
            await Task.WhenAll(tasks);

            // Process results, skipping non-existing keys
            var validKeys = tasks
                .Where(t => t.Result)
                .Select((t, index) => keys[index])
                .ToList();

            if (!validKeys.Any()) return Enumerable.Empty<TModel>();

            // Get member names from expressions
            var memberNames = expressions
                .Select(exp => MemberSelector.GetMemberName(exp))
                .Select(name => (RedisValue)name)
                .ToArray();

            // Create a new batch for fetching data
            batch = db.CreateBatch();
            var valueTasks = validKeys
                .Select(key => batch.HashGetAsync(key, memberNames))
                .ToList();

            batch.Execute();
            var valuesArray = await Task.WhenAll(valueTasks);

            var results = new List<TModel>();

            // Process each valid key
            for (int i = 0; i < valuesArray.Length; i++)
            {
                var values = valuesArray[i];
                var id = validKeys[i];

                var result = ModelFactory.Create<TModel>(id);

                // Map values to the corresponding properties
                for (int j = 0; j < memberNames.Length; j++)
                {
                    var memberName = memberNames[j];
                    var value = values[j];

                    if (value.HasValue)
                    {
                        var property = typeof(TModel).GetProperty(memberName);

                        if (property != null && property.CanWrite)
                        {
                            var convertedValue = value.DeserializeFromRedis(property.PropertyType);
                            property.SetValue(result, convertedValue);
                        }
                    }
                }

                results.Add(result);
            }

            return results;
        }
        #endregion

    }
}
