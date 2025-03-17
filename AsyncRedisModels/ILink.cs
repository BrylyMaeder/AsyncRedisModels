using AsyncRedisModels.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels
{
    public interface ILink<TModel> where TModel : IAsyncModel
    {
        string LinkedId { get; set; }
    }
}
