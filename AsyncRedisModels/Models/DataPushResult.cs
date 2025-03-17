using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels.Models
{
    public struct ModelPushResult<TModel>
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public string PropertyName { get; set; }
        public TModel Data { get; set; }
    }
}
