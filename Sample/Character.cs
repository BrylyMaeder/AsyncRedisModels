using AsyncRedisModels.Attributes;
using AsyncRedisModels.Contracts;
using AsyncRedisModels.Index;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels
{
    public class Character : IAsyncModel
    {
        [Indexed(IndexType.Tag)]
        public string Username { get; set; }
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }

        public string IndexName()
        {
            return "character";
        }

        public bool ChangeName() 
        {
            return false;
        }
    }
}
