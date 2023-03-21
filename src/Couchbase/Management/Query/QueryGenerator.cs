using Couchbase.Core.Utils;
using System.Collections.Generic;

namespace Couchbase.Management.Query
{
    internal static class QueryGenerator
    {
        private const string Default = "_default";
        public static string CreateIndexStatement(string bucketName, string indexName, IEnumerable<string> fields, CreateQueryIndexOptions options)
        {
            if(options.ScopeNameValue == null)
            {
                return $"CREATE INDEX {indexName.EscapeIfRequired()} ON {bucketName.EscapeIfRequired()}({string.Join(",", fields)}) USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
            }
            return $"CREATE INDEX {indexName.EscapeIfRequired()} ON {bucketName.EscapeIfRequired()}.{options.ScopeNameValue.EscapeIfRequired()}.{options.CollectionNameValue.EscapeIfRequired()}({string.Join(",", fields)}) USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
        }

        public static string CreateDeferredIndexStatement(string bucketName, string indexName, BuildDeferredQueryIndexOptions options)
        {
            if(options.ScopeNameValue == null)
            {
                return $"BUILD INDEX ON {bucketName.EscapeIfRequired()}({indexName.EscapeIfRequired()}) USING GSI;";
            }
            return $"BUILD INDEX ON {bucketName.EscapeIfRequired()}.{options.ScopeNameValue.EscapeIfRequired()}.{options.CollectionNameValue.EscapeIfRequired()}({indexName.EscapeIfRequired()}) USING GSI;";
        }

        public static string CreatePrimaryIndexStatement(string bucketName, CreatePrimaryQueryIndexOptions options = null)
        {
            if (options.ScopeNameValue == null && options.IndexNameValue == null)
            {
                return $"CREATE PRIMARY INDEX ON {bucketName.EscapeIfRequired()} USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
            }
            if (options.ScopeNameValue != null && options.IndexNameValue == null)
            {
                return $"CREATE PRIMARY INDEX ON {bucketName.EscapeIfRequired()}.{options.ScopeNameValue.EscapeIfRequired()}.{options.CollectionNameValue.EscapeIfRequired()} USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
            }
            if (options.ScopeNameValue == null && options.IndexNameValue != null)
            {
                return $"CREATE PRIMARY INDEX {options.IndexNameValue!.EscapeIfRequired()} ON {bucketName.EscapeIfRequired()} USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
            }
            return $"CREATE PRIMARY INDEX {options.IndexNameValue!.EscapeIfRequired()} ON {bucketName.EscapeIfRequired()}.{options.ScopeNameValue.EscapeIfRequired()}.{options.CollectionNameValue.EscapeIfRequired()} USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
        }

        public static string CreateDropIndexStatement(string bucketName, string indexName, DropQueryIndexOptions options)
        {
            if (options.ScopeNameValue == null)
            {
                return $"DROP INDEX {bucketName.EscapeIfRequired()}.{indexName.EscapeIfRequired()} USING GSI;";
            }
            return $"DROP INDEX {indexName.EscapeIfRequired()} ON {bucketName.EscapeIfRequired()}.{options.ScopeNameValue.EscapeIfRequired()}.{options.CollectionNameValue.EscapeIfRequired()} USING GSI;";
        }

        public static string CreateDropPrimaryIndexStatement(string bucketName, DropPrimaryQueryIndexOptions options)
        {
            if (options.ScopeNameValue == null)
            {
                return $"DROP PRIMARY INDEX ON {bucketName.EscapeIfRequired()} USING GSI;";
            }
            return $"DROP PRIMARY INDEX ON {bucketName.EscapeIfRequired()}.{options.ScopeNameValue.EscapeIfRequired()}.{options.CollectionNameValue.EscapeIfRequired()} USING GSI;";
        }

        public static string CreateGetAllIndexesStatement(GetAllQueryIndexOptions options)
        {
            if (options.CollectionNameValue != null && options.CollectionNameValue == "_default")
            {
                return DefaultCollectionAllIndexesStatement;
            }

            if (options.CollectionNameValue != null && options.CollectionNameValue != "_default")
            {
                return NonDefaultCollectionAllIndexesStatement;
            }

            if (options.ScopeNameValue != null && options.ScopeNameValue == "_default")
            {
                return BucketLevelAllIndexesStatement;
            }

            return BucketLevelAllIndexesStatement;
        }

        //If the collection is a default collection (e.g. appears on scope _default, collection _default), then a special case statement must be used to retrieve indexes:
        private const string DefaultCollectionAllIndexesStatement =
            "SELECT idx.* FROM system:indexes AS idx WHERE ((bucket_id=$bucketName AND scope_id=$scopeName AND keyspace_id=$collectionName) OR (bucket_id IS MISSING and keyspace_id=$bucketName)) AND `using`=\"gsi\" ORDER BY is_primary DESC, name ASC";

        //If the collection is not the default collection
        private const string NonDefaultCollectionAllIndexesStatement =
            "SELECT idx.* FROM system:indexes AS idx WHERE (bucket_id=$bucketName AND scope_id=$scopeName AND keyspace_id=$collectionName) AND `using`=\"gsi\" ORDER BY is_primary DESC, name ASC";

        //All indexes for all scopes and collections in a bucket
        private const string BucketLevelAllIndexesStatement =
            "SELECT idx.* FROM system:indexes AS idx WHERE ((bucket_id IS MISSING AND keyspace_id = $bucketName) OR bucket_id = $bucketName) AND `using`=\"gsi\" ORDER BY is_primary DESC, name ASC";
    }
}
