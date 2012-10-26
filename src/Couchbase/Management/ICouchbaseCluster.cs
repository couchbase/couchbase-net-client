using System;
using System.IO;

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
		/// Get a single bucket
		/// </summary>
		/// <param name="buckeName">Bucket name</param>
		/// <returns>Instance of a Bucket</returns>
		Bucket GetBucket(string bucketName);

		/// <summary>
		/// Get a single bucket
		/// </summary>
		/// <param name="buckeName">Bucket name</param>
		/// <param name="bucket">Bucket to return</param>
		/// <returns>True if bucket is found</returns>
		bool TryGetBucket(string bucketName, out Bucket bucket);

		/// <summary>
		/// Get count of items for a given bucket
		/// </summary>
		/// <param name="bucketName">Bucket name</param>
		/// <returns>Count of items</returns>
		long GetItemCount(string bucketName);

		/// <summary>
		/// Get count of items for all buckets
		/// </summary>
		/// <returns>Count of items</returns>
		long GetItemCount();

		/// <summary>
		/// Flush all data from a bucket
		/// </summary>
		/// <param name="bucketName">bucket name</param>
		void FlushBucket(string bucketName);

		/// <summary>
		/// Create a new bucket on the server
		/// </summary>
		/// <param name="bucket">Bucket to create</param>
		/// <returns>True if successful</returns>
		void CreateBucket(Bucket bucket);

		/// <summary>
		/// Delete a bucket on the server
		/// </summary>
		/// <param name="bucket">Bucket to create</param>
		/// <returns>True if successful</returns>
		void DeleteBucket(string bucketName);

		/// <summary>
		/// Create a new design document on the cluster
		/// </summary>
		/// <param name="bucket">The name of the bucket</param>
		/// <param name="name">The name of the design document, less the _design</param>
		/// <param name="document">The JSON body of the document</param>
		bool CreateDesignDocument(string bucket, string name, string document);

		/// <summary>
		/// Create a new design document on the cluster
		/// </summary>
		/// <param name="bucket">The name of the bucket</param>
		/// <param name="name">The name of the design document, less the _design</param>
		/// <param name="source">A stream that can be read and contains a JSON document</param>
		bool CreateDesignDocument(string bucket, string name, Stream source);

		/// <summary>
		/// Retrieve an existing design document from the cluster
		/// </summary>
		/// <param name="bucket">The name of the bucket</param>
		/// <param name="name">The name of the design document, less the _design</param>
		string RetrieveDesignDocument(string bucket, string name);

		/// <summary>
		/// Delete an existing design document from the cluster
		/// </summary>
		/// <param name="bucket">The name of the bucket</param>
		/// <param name="name">The name of the design document, less the _design</param>
		bool DeleteDesignDocument(string bucket, string name);
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