using System;
using System.Collections.Generic;
using Couchbase.Authentication;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    /// <summary>
    /// An intermediate class for doing management operations on a Cluster.
    /// </summary>
    public interface IClusterManager : IDisposable, IUserManager
    {
        /// <summary>
        /// Adds a node to the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        IResult AddNode(string ipAddress);

        /// <summary>
        /// Adds a node to the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        Task<IResult> AddNodeAsync(string ipAddress);

        /// <summary>
        /// Removes a failed over node from the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        /// <remarks>The node must have been failed over before removing or else this operation will fail.</remarks>
        IResult RemoveNode(string ipAddress);

        /// <summary>
        /// Removes a failed over node from the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        /// <remarks>The node must have been failed over before removing or else this operation will fail.</remarks>
        Task<IResult> RemoveNodeAsync(string ipAddress);

        /// <summary>
        /// Initiates a rebalance across the cluster.
        /// </summary>
        /// <returns>A boolean value indicating the result.</returns>
        IResult Rebalance();

        /// <summary>
        /// Initiates a rebalance across the cluster.
        /// </summary>
        /// <returns>A boolean value indicating the result.</returns>
        Task<IResult> RebalanceAsync();

        /// <summary>
        /// Returns the current state of the cluster.
        /// </summary>
        /// <returns></returns>
        IResult<IClusterInfo> ClusterInfo();

        /// <summary>
        /// Returns the current state of the cluster.
        /// </summary>
        /// <returns></returns>
        Task<IResult<IClusterInfo>> ClusterInfoAsync();

        /// <summary>
        /// List all current buckets in this cluster.
        /// </summary>
        /// <returns>A list of buckets and their properties.</returns>
        IResult<IList<BucketConfig>> ListBuckets();


        /// <summary>
        /// List all current buckets in this cluster.
        /// </summary>
        /// <returns>A list of buckets and their properties.</returns>
        Task<IResult<IList<BucketConfig>>> ListBucketsAsync();

        /// <summary>
        /// Creates a new bucket on the cluster
        /// </summary>
        /// <param name="name">Required parameter. Name for new bucket.</param>
        /// <param name="ramQuota">The RAM quota in megabytes. The default is 100.</param>
        /// <param name="bucketType">Required parameter. Type of bucket to be created. “Memcached” configures as Memcached bucket. “Couchbase” configures as Couchbase bucket</param>
        /// <param name="replicaNumber">The number of replicas of each document: minimum 0, maximum 3.</param>
        /// <param name="authType">The authentication type.</param>
        /// <param name="indexReplicas">Disable or enable indexes for bucket replicas.</param>
        /// <param name="flushEnabled">Enables the flush functionality on the specified bucket.</param>
        /// <param name="parallelDbAndViewCompaction">Indicates whether database and view files on disk can be compacted simultaneously.</param>
        /// <param name="saslPassword">Optional Parameter. String. Password for SASL authentication. Required if SASL authentication has been enabled.</param>
        /// <param name="threadNumber">Optional Parameter. Integer from 2 to 8. Change the number of concurrent readers and writers for the data bucket. </param>
        /// <returns>A boolean value indicating the result.</returns>
        IResult CreateBucket(string name, uint ramQuota = 100, BucketTypeEnum bucketType = BucketTypeEnum.Couchbase, ReplicaNumber replicaNumber = ReplicaNumber.Two, AuthType authType = AuthType.Sasl, bool indexReplicas = false, bool flushEnabled = false, bool parallelDbAndViewCompaction = false, string saslPassword = "", ThreadNumber threadNumber = ThreadNumber.Three);


        /// <summary>
        /// Creates a new bucket on the cluster
        /// </summary>
        /// <param name="name">Required parameter. Name for new bucket.</param>
        /// <param name="ramQuota">The RAM quota in megabytes. The default is 100.</param>
        /// <param name="bucketType">Required parameter. Type of bucket to be created. “Memcached” configures as Memcached bucket. “Couchbase” configures as Couchbase bucket</param>
        /// <param name="replicaNumber">The number of replicas of each document: minimum 0, maximum 3.</param>
        /// <param name="authType">The authentication type.</param>
        /// <param name="indexReplicas">Disable or enable indexes for bucket replicas.</param>
        /// <param name="flushEnabled">Enables the flush functionality on the specified bucket.</param>
        /// <param name="parallelDbAndViewCompaction">Indicates whether database and view files on disk can be compacted simultaneously.</param>
        /// <param name="saslPassword">Optional Parameter. String. Password for SASL authentication. Required if SASL authentication has been enabled.</param>
        /// <param name="threadNumber">Optional Parameter. Integer from 2 to 8. Change the number of concurrent readers and writers for the data bucket. </param>
        /// <returns>A boolean value indicating the result.</returns>
        Task<IResult> CreateBucketAsync(string name, uint ramQuota = 100, BucketTypeEnum bucketType = BucketTypeEnum.Couchbase, ReplicaNumber replicaNumber = ReplicaNumber.Two, AuthType authType = AuthType.Sasl, bool indexReplicas = false, bool flushEnabled = false, bool parallelDbAndViewCompaction = false, string saslPassword = "", ThreadNumber threadNumber = ThreadNumber.Three);

        /// <summary>
        /// Creates a new bucket on the cluster
        /// </summary>
        /// <param name="settings">The settings for the bucket.</param>
        /// <returns></returns>
       Task<IResult> CreateBucketAsync(BucketSettings settings);
        /// <summary>
        /// Removes a bucket from the cluster permamently.
        /// </summary>
        /// <param name="name">The name of the bucket.</param>
        /// <returns>A boolean value indicating the result.</returns>
        IResult RemoveBucket(string name);

        /// <summary>
        /// Removes a bucket from the cluster permamently.
        /// </summary>
        /// <param name="name">The name of the bucket.</param>
        /// <returns>A boolean value indicating the result.</returns>
        Task<IResult> RemoveBucketAsync(string name);

        /// <summary>
        /// Fails over a given node
        /// </summary>
        /// <param name="hostname">The name of the node to remove.</param>
        /// <returns>A boolean value indicating the result.</returns>
        IResult FailoverNode(string hostname);

        /// <summary>
        /// Fails over a given node
        /// </summary>
        /// <param name="hostname">The name of the node to remove.</param>
        /// <returns>A boolean value indicating the result.</returns>
        Task<IResult> FailoverNodeAsync(string hostname);

        /// <summary>
        /// Initializes the entry point (EP) node of the cluster; similar to using the Management Console to setup a cluster.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="path">The path to the data file. The default is "/opt/couchbase/var/lib/couchbase/data".</param>
        /// <param name="indexPath">The index path to data file. The default is "/opt/couchbase/var/lib/couchbase/data".</param>
        /// <remarks>See: <a href="http://docs.couchbase.com/admin/admin/Misc/admin-datafiles.html"/></remarks>
        /// <returns>An <see cref="IResult"/> with the status of the operation.</returns>
        Task<IResult> InitializeClusterAsync(string hostName = "127.0.0.1", string path = "/opt/couchbase/var/lib/couchbase/data", string indexPath = "/opt/couchbase/var/lib/couchbase/data");

        /// <summary>
        /// Renames the name of a node from it's default.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// In most cases this should just be the IP or hostname of node.
        /// <returns>An <see cref="IResult"/> with the status of the operation.</returns>
        Task<IResult> RenameNodeAsync(string hostName);

        /// <summary>
        /// Sets up the services that are available on a given node.
        /// </summary>
        /// <param name="hostName">The hostname or IP of the node.</param>
        /// <param name="services">The services - e.g. query, kv, and/or index</param>
        /// <returns>An <see cref="IResult"/> with the status of the operation.</returns>
        Task<IResult> SetupServicesAsync(string hostName, List<CouchbaseService> services);

        /// <summary>
        /// Sets up the services that are available on a given node.
        /// </summary>
        /// <param name="hostName">The hostname or IP of the node.</param>
        /// <param name="services">The services - e.g. query, kv, and/or index</param>
        /// <returns>An <see cref="IResult"/> with the status of the operation.</returns>
        Task<IResult> SetupServicesAsync(string hostName, params CouchbaseService[] services);

        /// <summary>
        /// Provisions the memory for an EP node.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="dataMemQuota">The data memory quota.</param>
        /// <param name="indexMemQuota"></param>
        /// <returns></returns>
        Task<IResult> ConfigureMemoryAsync(string hostName, uint dataMemQuota, uint indexMemQuota);

        /// <summary>
        /// Provisions the administartor account for an EP node.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <returns></returns>
        Task<IResult> ConfigureAdminAsync(string hostName);

        /// <summary>
        /// Adds the sample bucket.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <param name="sampleBucketName">Name of the sample bucket.</param>
        /// <returns></returns>
        Task<IResult> AddSampleBucketAsync(string hostname, string sampleBucketName);

        /// <summary>
        /// Adds a node to the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <param name="services">The services.</param>
        /// <returns></returns>
        Task<IResult> AddNodeAsync(string ipAddress, params CouchbaseService[] services);
    }
}
