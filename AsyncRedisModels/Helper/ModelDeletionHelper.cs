using AsyncRedisModels.Contracts;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncRedisModels.Helper
{
    internal class ModelDeletionHelper
    {
        internal static async Task CollectDeletionTasksAsync(IAsyncModel model, List<Task> deletionTasks)
        {
            if (model == null) return;

            // Add model's own deletion
            deletionTasks.Add(model.DeleteAsync());

            foreach (var property in model.GetType().GetProperties().Where(p => p.CanRead))
            {
                var value = property.GetValue(model);

                if (value is IAsyncModel nestedModel)
                {
                    await CollectDeletionTasksAsync(nestedModel, deletionTasks);
                }
                else if (value is IDeletable deletable)
                {
                    deletionTasks.Add(deletable.DeleteAsync());
                }
            }

            // Add listener task if applicable
            if (model is IDeletionListener listener)
            {
                deletionTasks.Add(listener.OnDeleted());
            }
        }

        internal static async Task CleanupRedisKeysAsync(IAsyncModel model)
        {
            const int batchSize = 100;
            var cursor = 0L;

            try
            {
                do
                {
                    var scanResult = await RedisSingleton.Database.ExecuteAsync("SCAN",
                        cursor.ToString(), "MATCH", $"{model.GetKey()}*", "COUNT", batchSize);

                    var resultArray = (RedisResult[])scanResult;
                    cursor = long.Parse(resultArray[0].ToString());
                    var keys = ((RedisResult[])resultArray[1]).Select(r => (RedisKey)r).ToArray();

                    if (keys.Any())
                    {
                        await RedisSingleton.Database.KeyDeleteAsync(keys);
                    }
                } while (cursor != 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during deletion: {ex.Message}");
            }
        }
    }
}
