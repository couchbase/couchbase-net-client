using System.Collections.Generic;

namespace Couchbase.Core.Configuration.Server
{
    internal class CollectionDef
    {
        public string name { get; set; }
        public string uid { get; set; }
    }

    internal class ScopeDef
    {
        public string name { get; set; }
        public string uid { get; set; }
        public List<CollectionDef> collections { get; set; }
    }

    internal class Manifest
    {
        public string uid { get; set; }
        public List<ScopeDef> scopes { get; set; }
    }
}
