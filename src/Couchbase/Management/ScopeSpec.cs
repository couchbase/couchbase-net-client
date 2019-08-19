using System.Collections.Generic;

namespace Couchbase.Management
{
    public class ScopeSpec
    {
        public string Name { get; }
        public IEnumerable<CollectionSpec> Collections { get; set; }

        public ScopeSpec(string name)
        {
            Name = name;
        }
    }
}