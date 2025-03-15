using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncRedisModels.Factory;
using AsyncRedisModels.Contracts;

namespace AsyncRedisModels.Query
{
    public static class QueryExtensions
    {
        public static async Task<(List<TModel> Documents, int TotalCount, int TotalPages)> ToPagedListAsync<TModel>(this RedisQuery<TModel> query, int page = 1, int pageSize = 1000) where TModel : IAsyncModel
        {
            var results = await RedisSearchFunctions.Execute(query, page, pageSize);

            var documents = results.DocumentIds.Select(s => ModelFactory.Create<TModel>(s)).ToList();

            return (documents, results.TotalCount, results.TotalPages);
        }

        public static async Task<List<TModel>> ToListAsync<TModel>(this RedisQuery<TModel> builder, int page = 1, int pageSize = 1000) where TModel : IAsyncModel
        {
            var results = await RedisSearchFunctions.Execute(builder, page, pageSize);

            return results.DocumentIds.Select(s => ModelFactory.Create<TModel>(s)).ToList();
        }

        public static async Task<bool> AnyAsync<TModel>(this RedisQuery<TModel> query) where TModel : IAsyncModel
        {
            var results = await RedisSearchFunctions.Execute(query, 1, 1);

            return results.DocumentIds.Count > 0;
        }

        internal static async Task<List<string>> SearchAsync<TModel>(this RedisQuery<TModel> query, int page = 1, int pageSize = 1000) where TModel : IAsyncModel
        {
            var results = await RedisSearchFunctions.Execute(query, page, pageSize);
            return results.DocumentIds;
        }

        public static async Task<List<TModel>> SelectAsync<TModel>(
            this RedisQuery<TModel> query,
            params Expression<Func<TModel, object>>[] propertyExpressions)
            where TModel : IAsyncModel, new()
        {
            // Extract the fields from the expressions
            var selectedFields = propertyExpressions
                .SelectMany(expr => RedisSearchFunctions.GetSelectedFields(expr))
                .Distinct()
                .ToList();

            var results = await RedisSearchFunctions.SelectAsync<TModel>(query, selectedFields, 1, 1000);

            return results.models;
        }

        public static async Task<(List<TModel> Results, int TotalCount, int TotalPages)> PagedSelectAsync<TModel>(
            this RedisQuery<TModel> query, int pageNumber, int pageSize,
            params Expression<Func<TModel, object>>[] propertyExpressions)
            where TModel : IAsyncModel, new()
        {
            // Extract the fields from the expressions
            var selectedFields = propertyExpressions
                .SelectMany(expr => RedisSearchFunctions.GetSelectedFields(expr))
                .Distinct()
                .ToList();

            return await RedisSearchFunctions.SelectAsync<TModel>(query, selectedFields, pageNumber, pageSize);
        }
    }
}
