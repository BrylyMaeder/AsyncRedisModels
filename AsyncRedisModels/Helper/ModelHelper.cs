using AsyncRedisModels.Factory;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels.Helper
{
    public static class ModelHelper
    {
        public static string GetIndex<TModel>() 
        {
            return ModelFactory.CreateEmpty(typeof(TModel)).IndexName();
        }

        public static string CreateKey<TModel>(string id) 
        {
            return $"{GetIndex<TModel>()}:{id}";
        }
    }
}
