using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Query
{
    public static class QueryIndexManagerExtensions
    {
        public static Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(this IQueryIndexManager queryIndexManager, string bucketName)
        {
            return queryIndexManager.GetAllIndexesAsync(bucketName, GetAllQueryIndexOptions.Default);
        }

        public static Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(this IQueryIndexManager queryIndexManager, string bucketName, Action<GetAllQueryIndexOptions> configureOptions)
        {
            var options = new GetAllQueryIndexOptions();
            configureOptions(options);

            return queryIndexManager.GetAllIndexesAsync(bucketName, options);
        }

        public static Task CreateIndexAsync(this IQueryIndexManager queryIndexManager, string bucketName, string indexName, IEnumerable<string> fields)
        {
            return queryIndexManager.CreateIndexAsync(bucketName, indexName, fields, CreateQueryIndexOptions.Default);
        }

        public static Task CreateIndexAsync(this IQueryIndexManager queryIndexManager, string bucketName, string indexName, IEnumerable<string> fields, Action<CreateQueryIndexOptions> configureOptions)
        {
            var options = new CreateQueryIndexOptions();
            configureOptions(options);

            return queryIndexManager.CreateIndexAsync(bucketName, indexName, fields, options);
        }

        public static Task CreatePrimaryIndexAsync(this IQueryIndexManager queryIndexManager, string bucketName)
        {
            return queryIndexManager.CreatePrimaryIndexAsync(bucketName, CreatePrimaryQueryIndexOptions.Default);
        }

        public static Task CreatePrimaryIndexAsync(this IQueryIndexManager queryIndexManager, string bucketName, Action<CreatePrimaryQueryIndexOptions> configureOptions)
        {
            var options = new CreatePrimaryQueryIndexOptions();
            configureOptions(options);

            return queryIndexManager.CreatePrimaryIndexAsync(bucketName, options);
        }

        public static Task DropIndexAsync(this IQueryIndexManager queryIndexManager, string bucketName, string indexName)
        {
            return queryIndexManager.DropIndexAsync(bucketName, indexName, DropQueryIndexOptions.Default);
        }

        public static Task DropIndexAsync(this IQueryIndexManager queryIndexManager, string bucketName, string indexName, Action<DropQueryIndexOptions> configureOptions)
        {
            var options = new DropQueryIndexOptions();
            configureOptions(options);

            return queryIndexManager.DropIndexAsync(bucketName, indexName, options);
        }

        public static Task DropPrimaryIndexAsync(this IQueryIndexManager queryIndexManager, string bucketName)
        {
            return queryIndexManager.DropPrimaryIndexAsync(bucketName, DropPrimaryQueryIndexOptions.Default);
        }

        public static Task DropPrimaryIndexAsync(this IQueryIndexManager queryIndexManager, string bucketName, Action<DropPrimaryQueryIndexOptions> configureOptions)
        {
            var options = new DropPrimaryQueryIndexOptions();
            configureOptions(options);

            return queryIndexManager.DropPrimaryIndexAsync(bucketName, options);
        }

        public static Task BuildDeferredIndexesAsync(this IQueryIndexManager queryIndexManager, string bucketName)
        {
            return queryIndexManager.BuildDeferredIndexesAsync(bucketName, BuildDeferredQueryIndexOptions.Default);
        }

        public static Task BuildDeferredIndexesAsync(this IQueryIndexManager queryIndexManager, string bucketName, Action<BuildDeferredQueryIndexOptions> configureOptions)
        {
            var options = new BuildDeferredQueryIndexOptions();
            configureOptions(options);

            return queryIndexManager.BuildDeferredIndexesAsync(bucketName, options);
        }

        public static Task WatchIndexesAsync(this IQueryIndexManager queryIndexManager, string bucketName, IEnumerable<string> indexNames)
        {
            return queryIndexManager.WatchIndexesAsync(bucketName, indexNames, WatchQueryIndexOptions.Default);
        }

        public static Task WatchIndexesAsync(this IQueryIndexManager queryIndexManager, string bucketName, IEnumerable<string> indexNames, Action<WatchQueryIndexOptions> configureOptions)
        {
            var options = new WatchQueryIndexOptions();
            configureOptions(options);

            return queryIndexManager.WatchIndexesAsync(bucketName, indexNames, options);
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
