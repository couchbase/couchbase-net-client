using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Buckets
{
    public interface IBucketManager
    {
        Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions? options = null);

        Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null);

        Task DropBucketAsync(string bucketName, DropBucketOptions? options = null);

        Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null);

        Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null);

        Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null);
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
