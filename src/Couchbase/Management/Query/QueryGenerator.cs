using Couchbase.Core.Utils;
using System.Collections.Generic;

namespace Couchbase.Management.Query
{
    internal class QueryGenerator
    {
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

        public static string CreateGetAllIndexesStatement(string bucketName, GetAllQueryIndexOptions options)
        {
            if(options.ScopeNameValue == null)
            {
                return $"SELECT i.* FROM system:indexes AS i WHERE i.keyspace_id={bucketName.EscapeIfRequired()} AND `using`=\"gsi\";";
            }
            return $"SELECT i.* FROM system:indexes AS i WHERE i.bucket_id={bucketName.EscapeIfRequired()} AND scope_id={options.ScopeNameValue.EscapeIfRequired()} AND i.keyspace_id={options.CollectionNameValue.EscapeIfRequired()} AND `using`=\"gsi\";";
        }
    }
}
