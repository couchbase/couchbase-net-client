using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Query
{
    public interface IQueryIndexManager
    {
        Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(string bucketName, GetAllQueryIndexOptions? options = null);

        Task CreateIndexAsync(string bucketName, string indexName, IEnumerable<string> fields, CreateQueryIndexOptions? options = null);

        Task CreatePrimaryIndexAsync(string bucketName, CreatePrimaryQueryIndexOptions? options = null);

        Task DropIndexAsync(string bucketName, string indexName, DropQueryIndexOptions? options = null);

        Task DropPrimaryIndexAsync(string bucketName, DropPrimaryQueryIndexOptions? options = null);

        Task BuildDeferredIndexesAsync(string bucketName, BuildDeferredQueryIndexOptions? options = null);

        Task WatchIndexesAsync(string bucketName, IEnumerable<string> indexNames, WatchQueryIndexOptions? options = null);
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
