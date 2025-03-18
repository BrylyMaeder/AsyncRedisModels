using AsyncRedisModels.Contracts;
using AsyncRedisModels.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AsyncRedisModels.Components
{
    public class ManagedLinks<TModel> : AsyncLinks<TModel>, IDeletable where TModel : IAsyncModel
    {
        public ManagedLinks(IAsyncModel document, [CallerMemberName] string propertyName = "") : base(document, propertyName)
        {

        }

        public override async Task SetAsync(List<TModel> documents)
        {
            await ClearAsync();

            await base.SetAsync(documents);
        }

        public override async Task<bool> RemoveAsync(string id)
        {
            var removed = await base.RemoveAsync(id);

            if (removed)
            {
                var document = ModelFactory.Create<TModel>(id);
                await document.DeleteAsync();
            }

            return removed;
        }

        public override async Task ClearAsync()
        {
            var documentIds = await RedisSingleton.Database.SetMembersAsync(_fullKey);

            foreach (var id in documentIds.Select(value => value.ToString()))
            {
                var document = ModelFactory.Create<TModel>(id);
                await document.DeleteAsync();
            }

            await base.ClearAsync();
        }

        public async Task DeleteAsync()
        {
            await ClearAsync();
        }
    }
}
