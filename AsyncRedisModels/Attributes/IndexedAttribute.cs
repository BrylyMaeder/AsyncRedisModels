using AsyncRedisModels.Index;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class IndexedAttribute : Attribute
    {
        public IndexType IndexType { get; }
        public bool UniqueValidation { get; }

        public IndexedAttribute(IndexType type = IndexType.Auto)
        {
            IndexType = type;
        }
    }
}
