using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = false)]
    public class HydrateAttribute : Attribute
    {
        public bool Enabled { get; }

        public HydrateAttribute(bool enabled = true) => Enabled = enabled;
    }


}
