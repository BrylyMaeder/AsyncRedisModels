using AsyncRedisDocuments.Components;
using AsyncRedisModels;
using AsyncRedisModels.Factory;
using AsyncRedisModels.Repository;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AsyncRedisDocuments
{
    public class LinkedModel<TDocument> : BaseComponent where TDocument : IAsyncModel
    {
        private AsyncDictionary<string, string> _links { get; set; }

        public LinkedModel(IAsyncModel document = null, [CallerMemberName] string propertyName = "") : base(document, propertyName) 
        {
            _links = new AsyncDictionary<string, string>(document, "links");
        }

        public virtual async Task SetAsync(string id)
        {
            await _links.SetAsync(_propertyName, id);
        }

        public async Task SetAsync(TDocument document)
        {
            if (document == null)
            {
                await ClearAsync();
                return;
            }

            await SetAsync(document.Id);
        }

        public async Task ClearAsync() 
        {
            await _links.RemoveAsync(_propertyName);
        }

        public async Task<string> GetIdAsync()
        {
            return await _links.GetByKeyAsync(_propertyName);
        }

        public async Task<TDocument> GetAsync()
        {
            return await RedisRepository.LoadAsync<TDocument>(await GetIdAsync());
        }
    }
}
