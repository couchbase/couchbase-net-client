using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Search
{
    public static class SearchIndexManagerExtensions
    {
        public static Task<SearchIndex> GetIndexAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.GetIndexAsync(indexName, GetSearchIndexOptions.Default);
        }

        public static Task<SearchIndex> GetIndexAsync(this ISearchIndexManager manager, string indexName, Action<GetSearchIndexOptions> configureOptions)
        {
            var options = new GetSearchIndexOptions();
            configureOptions(options);

            return manager.GetIndexAsync(indexName, options);
        }

        public static Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(this ISearchIndexManager manager)
        {
            return manager.GetAllIndexesAsync(GetAllSearchIndexesOptions.Default);
        }

        public static Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(this ISearchIndexManager manager, Action<GetAllSearchIndexesOptions> configureOptions)
        {
            var options = new GetAllSearchIndexesOptions();
            configureOptions(options);

            return manager.GetAllIndexesAsync(options);
        }

        public static Task UpsertIndexAsync(this ISearchIndexManager manager, SearchIndex indexDefinition)
        {
            return manager.UpsertIndexAsync(indexDefinition, UpsertSearchIndexOptions.Default);
        }

        public static Task UpsertIndexAsync(this ISearchIndexManager manager, SearchIndex indexDefinition, Action<UpsertSearchIndexOptions> configureOptions)
        {
            var options = new UpsertSearchIndexOptions();
            configureOptions(options);

            return manager.UpsertIndexAsync(indexDefinition, options);
        }

        public static Task DropIndexAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.DropIndexAsync(indexName, DropSearchIndexOptions.Default);
        }

        public static Task DropIndexAsync(this ISearchIndexManager manager, string indexName, Action<DropSearchIndexOptions> configureOptions)
        {
            var options = new DropSearchIndexOptions();
            configureOptions(options);

            return manager.DropIndexAsync(indexName, options);
        }

        public static Task<int> GetIndexedDocumentsCountAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.GetIndexedDocumentsCountAsync(indexName, GetSearchIndexDocumentCountOptions.Default);
        }

        public static Task<int> GetIndexedDocumentsCountAsync(this ISearchIndexManager manager, string indexName, Action<GetSearchIndexDocumentCountOptions> configureOptions)
        {
            var options = new GetSearchIndexDocumentCountOptions();
            configureOptions(options);

            return manager.GetIndexedDocumentsCountAsync(indexName, options);
        }

        public static Task PauseIngestAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.PauseIngestAsync(indexName, PauseIngestSearchIndexOptions.Default);
        }

        public static Task PauseIngestAsync(this ISearchIndexManager manager, string indexName, Action<PauseIngestSearchIndexOptions> configureOptions)
        {
            var options = new PauseIngestSearchIndexOptions();
            configureOptions(options);

            return manager.PauseIngestAsync(indexName, options);
        }

        public static Task ResumeIngestAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.ResumeIngestAsync(indexName, ResumeIngestSearchIndexOptions.Default);
        }

        public static Task ResumeIngestAsync(this ISearchIndexManager manager, string indexName, Action<ResumeIngestSearchIndexOptions> configureOptions)
        {
            var options = new ResumeIngestSearchIndexOptions();
            configureOptions(options);

            return manager.ResumeIngestAsync(indexName, options);
        }

        public static Task AllowQueryingAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.AllowQueryingAsync(indexName, AllowQueryingSearchIndexOptions.Default);
        }

        public static Task AllowQueryingAsync(this ISearchIndexManager manager, string indexName, Action<AllowQueryingSearchIndexOptions> configureOptions)
        {
            var options = new AllowQueryingSearchIndexOptions();
            configureOptions(options);

            return manager.AllowQueryingAsync(indexName, options);
        }

        public static Task DisallowQueryingAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.DisallowQueryingAsync(indexName, DisallowQueryingSearchIndexOptions.Default);
        }

        public static Task DisallowQueryingAsync(this ISearchIndexManager manager, string indexName, Action<DisallowQueryingSearchIndexOptions> configureOptions)
        {
            var options = new DisallowQueryingSearchIndexOptions();
            configureOptions(options);

            return manager.DisallowQueryingAsync(indexName, options);
        }

        public static Task FreezePlanAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.FreezePlanAsync(indexName, FreezePlanSearchIndexOptions.Default);
        }

        public static Task FreezePlanAsync(this ISearchIndexManager manager, string indexName, Action<FreezePlanSearchIndexOptions> configureOptions)
        {
            var options = new FreezePlanSearchIndexOptions();
            configureOptions(options);

            return manager.FreezePlanAsync(indexName, options);
        }

        public static Task UnfreezePlanAsync(this ISearchIndexManager manager, string indexName)
        {
            return manager.UnfreezePlanAsync(indexName, UnfreezePlanSearchIndexOptions.Default);
        }

        public static Task UnfreezePlanAsync(this ISearchIndexManager manager, string indexName, Action<UnfreezePlanSearchIndexOptions> configureOptions)
        {
            var options = new UnfreezePlanSearchIndexOptions();
            configureOptions(options);

            return manager.UnfreezePlanAsync(indexName, options);
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
