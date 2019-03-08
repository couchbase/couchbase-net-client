using System.Collections.Generic;

namespace Couchbase.Core.Configuration.Server
{
    public class CollectionDef
    {
        public string name { get; set; }
        public string uid { get; set; }
    }

    public class ScopeDef
    {
        public string name { get; set; }
        public string uid { get; set; }
        public List<CollectionDef> collections { get; set; }
    }

    public class Manifest
    {
        public string uid { get; set; }
        public List<ScopeDef> scopes { get; set; }
    }
}
