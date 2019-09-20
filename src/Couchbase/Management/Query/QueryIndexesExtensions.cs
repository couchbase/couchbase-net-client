using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Query
{
    public static class QueryIndexesExtensions
    {
        public static Task<IEnumerable<QueryIndex>> GetAllAsync(this IQueryIndexes queryIndexes, string bucketName)
        {
            return queryIndexes.GetAllAsync(bucketName, GetAllQueryIndexOptions.Default);
        }

        public static Task<IEnumerable<QueryIndex>> GetAllAsync(this IQueryIndexes queryIndexes, string bucketName, Action<GetAllQueryIndexOptions> configureOptions)
        {
            var options = new GetAllQueryIndexOptions();
            configureOptions(options);

            return queryIndexes.GetAllAsync(bucketName, options);
        }

        public static Task CreateAsync(this IQueryIndexes queryIndexes, string bucketName, string indexName, IEnumerable<string> fields)
        {
            return queryIndexes.CreateAsync(bucketName, indexName, fields, CreateQueryIndexOptions.Default);
        }

        public static Task CreateAsync(this IQueryIndexes queryIndexes, string bucketName, string indexName, IEnumerable<string> fields, Action<CreateQueryIndexOptions> configureOptions)
        {
            var options = new CreateQueryIndexOptions();
            configureOptions(options);

            return queryIndexes.CreateAsync(bucketName, indexName, fields, options);
        }

        public static Task CreatePrimaryAsync(this IQueryIndexes queryIndexes, string bucketName)
        {
            return queryIndexes.CreatePrimaryAsync(bucketName, CreatePrimaryQueryIndexOptions.Default);
        }

        public static Task CreatePrimaryAsync(this IQueryIndexes queryIndexes, string bucketName, Action<CreatePrimaryQueryIndexOptions> configureOptions)
        {
            var options = new CreatePrimaryQueryIndexOptions();
            configureOptions(options);

            return queryIndexes.CreatePrimaryAsync(bucketName, options);
        }

        public static Task DropAsync(this IQueryIndexes queryIndexes, string bucketName, string indexName)
        {
            return queryIndexes.DropAsync(bucketName, indexName, DropQueryIndexOptions.Default);
        }

        public static Task DropAsync(this IQueryIndexes queryIndexes, string bucketName, string indexName, Action<DropQueryIndexOptions> configureOptions)
        {
            var options = new DropQueryIndexOptions();
            configureOptions(options);

            return queryIndexes.DropAsync(bucketName, indexName, options);
        }

        public static Task DropPrimaryAsync(this IQueryIndexes queryIndexes, string bucketName)
        {
            return queryIndexes.DropPrimaryAsync(bucketName, DropPrimaryQueryIndexOptions.Default);
        }

        public static Task DropPrimaryAsync(this IQueryIndexes queryIndexes, string bucketName, Action<DropPrimaryQueryIndexOptions> configureOptions)
        {
            var options = new DropPrimaryQueryIndexOptions();
            configureOptions(options);

            return queryIndexes.DropPrimaryAsync(bucketName, options);
        }

        public static Task BuildDeferredAsync(this IQueryIndexes queryIndexes, string bucketName)
        {
            return queryIndexes.BuildDeferredAsync(bucketName, BuildDeferredQueryIndexOptions.Default);
        }

        public static Task BuildDeferredAsync(this IQueryIndexes queryIndexes, string bucketName, Action<BuildDeferredQueryIndexOptions> configureOptions)
        {
            var options = new BuildDeferredQueryIndexOptions();
            configureOptions(options);

            return queryIndexes.BuildDeferredAsync(bucketName, options);
        }

        public static Task WatchAsync(this IQueryIndexes queryIndexes, string bucketName, IEnumerable<string> indexNames)
        {
            return queryIndexes.WatchAsync(bucketName, indexNames, WatchQueryIndexOptions.Default);
        }

        public static Task WatchAsync(this IQueryIndexes queryIndexes, string bucketName, IEnumerable<string> indexNames, Action<WatchQueryIndexOptions> configureOptions)
        {
            var options = new WatchQueryIndexOptions();
            configureOptions(options);

            return queryIndexes.WatchAsync(bucketName, indexNames, options);
        }
    }
}
