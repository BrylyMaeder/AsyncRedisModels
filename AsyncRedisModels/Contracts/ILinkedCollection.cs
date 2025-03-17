using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AsyncRedisModels.Contracts
{
    public interface ILinkedCollection<TModel> where TModel : IAsyncModel
    {
        Task AddAsync(TModel model);
        Task RemoveAsync(string modelId); 
        Task<TModel> GetAsync(string modelId); 
        Task<IEnumerable<TModel>> GetAllAsync(); 
    }

}
