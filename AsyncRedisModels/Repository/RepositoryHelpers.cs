using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using System.Text;
using AsyncRedisModels.Contracts;
using AsyncRedisModels.Extensions;
using AsyncRedisModels.Factory;
using AsyncRedisModels.Attributes;
using System.Threading.Tasks;

namespace AsyncRedisModels.Repository
{
    public partial class RedisRepository
    {
        private static RedisValue[] GetMemberNames<TModel>(Expression<Func<TModel, object>>[] expressions)
        {
            return expressions.Any()
                ? expressions.Select(exp => MemberSelector.GetMemberName(exp)).Select(name => (RedisValue)name).ToArray()
                : typeof(TModel).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(p => (RedisValue)p.Name).ToArray();
        }
    }
}
