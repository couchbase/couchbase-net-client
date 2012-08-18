using System;

namespace Couchbase.Management
{
	interface ICouchbaseCluster
	{
		/// <summary>
		/// List the buckets from the cluster
		/// </summary>
		/// <returns>An array of Bucket instances</returns>
		Bucket[] ListBuckets();

		/// <summary>
		/// List the buckets from the cluster, swallowing possible exceptions
		/// </summary>
		/// <returns>An array of Bucket instances</returns>
		bool TryListBuckets(out Bucket[] buckets);

		/// <summary>
		/// Flush all data from a bucket
		/// </summary>
		/// <param name="bucketName">bucket name</param>
		void FlushBucket(string bucketName);
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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
#endregion