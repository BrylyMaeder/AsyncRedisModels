using AsyncRedisModels;
using AsyncRedisModels.Attributes;
using AsyncRedisModels.Contracts;
using AsyncRedisModels.Index;
using Sample;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels
{
    [Hydrate]
    public class Character : IAsyncModel
    {
        [Unique]
        public string Username { get; set; }
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }

        [Hydrate]
        public NestedObject myNestedObject { get; set; }

        public AsyncLinks<NestedObject> LinkedList => new AsyncLinks<NestedObject>(this);
        public AsyncLinks<NestedObject> LinkedObject => new AsyncLinks<NestedObject>(this);

        public string IndexName()
        {
            return "character";
        }

        public async Task<bool> ChangeUsername(string username)
        {
            //Ensure most updated data
            await this.PullAsync(s => s.Username);
            var oldUsername = Username;

            Username = username;
            var pushResult = await this.PushAsync(s => s.Username);
            if (!pushResult.Succeeded)
            {
                Console.WriteLine(pushResult.Message);
                Username = oldUsername;
                return false;
            }

            return true;
        }
    }
}
