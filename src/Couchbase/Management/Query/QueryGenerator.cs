using Couchbase.Core.Utils;
using System.Collections.Generic;

namespace Couchbase.Management.Query
{
    internal class QueryGenerator
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
                return $"BUILD INDEX ON {bucketName.EscapeIfRequired()}({indexName}) USING GSI;";
            }
            return $"BUILD INDEX ON {bucketName.EscapeIfRequired()}.{options.ScopeNameValue.EscapeIfRequired()}.{options.CollectionNameValue.EscapeIfRequired()}({indexName}) USING GSI;";
        }

        public static string CreatePrimaryIndexStatement(string bucketName, CreatePrimaryQueryIndexOptions options = null)
        {
            if (options.ScopeNameValue == null)
            {
                return $"CREATE PRIMARY INDEX ON {bucketName.EscapeIfRequired()} USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
            }
            return $"CREATE PRIMARY INDEX ON {bucketName.EscapeIfRequired()}.{options.ScopeNameValue.EscapeIfRequired()}.{options.CollectionNameValue.EscapeIfRequired()} USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
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
            var bucketCondition = "(bucket_id = $bucketName)";
            var scopeCondition = "(" + bucketCondition + " AND scope_id = $scopeName)";
            var collectionCondition = "(" + scopeCondition + " AND keyspace_id = $collectionName)";

            string whereCondition;
            if (options.CollectionNameValue != null)
                whereCondition = collectionCondition;
            else if(options.ScopeNameValue != null)
                whereCondition = scopeCondition;
            else
                whereCondition = bucketCondition;

            if(Default.Equals(options.CollectionNameValue) ||
                string.IsNullOrWhiteSpace(options.CollectionNameValue))
            {
                var defaultCollectionCondition = "(bucket_id IS MISSING AND keyspace_id = $bucketName)";
                whereCondition = "(" + whereCondition + " OR " + defaultCollectionCondition + ")";
            }

            return "SELECT idx.* FROM system:indexes AS idx" +
                    " WHERE " + whereCondition +
                    " AND `using` = \"gsi\"" +
                    " ORDER BY is_primary DESC, name ASC";
        }
    }
}
