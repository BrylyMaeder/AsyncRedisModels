using AsyncRedisModels.Contracts;
using AsyncRedisModels.Helper;
using AsyncRedisModels.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace AsyncRedisModels.Repository
{
    public partial class RedisRepository
    {
        public static RedisQuery<TModel> Query<TModel>(Expression<Func<TModel, bool>> expression) where TModel : IAsyncModel 
        {
            var index = ModelHelper.GetIndex<TModel>();

            var query = new RedisQuery<TModel>(index);

            query = query.Where(expression);

            return query;
        }
    }
}
