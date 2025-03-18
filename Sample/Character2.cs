using AsyncRedisModels;
using AsyncRedisModels.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample
{
    public class Character2 : IAsyncModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime CreatedAt { get; set; }
        public bool IsReal { get; set; }
        public string IndexName()
        {
            return "character2";
        }
    }
}
