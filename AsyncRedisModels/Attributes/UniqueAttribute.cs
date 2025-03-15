using AsyncRedisModels.Index;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels.Attributes
{
    public class UniqueAttribute : IndexedAttribute
    {
        public UniqueAttribute(IndexType indexType = IndexType.Tag) : base(indexType) { }
    }
}
