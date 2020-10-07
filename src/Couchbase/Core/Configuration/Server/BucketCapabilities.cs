using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Configuration.Server
{
    public static class BucketCapabilities
    {
        public const string CBHELLO = "cbhello";
        public const string TOUCH = "touch";
        public const string COUCHAPI = "couchapi";
        public const string CCCP = "cccp";
        public const string XDCR_CHECKPOINTING = "xdcrCheckpointing";
        public const string NODES_EXT = "nodesExt";
        public const string DCP = "dcp";
        public const string XATTR = "xattr";
        public const string SNAPPY = "snappy";
        public const string COLLECTIONS = "collections";
        public const string DURABLE_WRITE = "durableWrite";
        public const string CREATE_AS_DELETED = "tombstonedUserXAttrs";
    }
}
