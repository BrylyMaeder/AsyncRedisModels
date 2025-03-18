using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels.Models
{

    public struct ModelCreationResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public struct ModelCreationResult<TModel>
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public TModel Data { get; set; }
    }
}
