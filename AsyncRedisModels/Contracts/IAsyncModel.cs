using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels.Contracts
{
    public interface IAsyncModel
    {
        string Id { get; set; }
        DateTime CreatedAt { get; set; }
        string IndexName();
    }
}
