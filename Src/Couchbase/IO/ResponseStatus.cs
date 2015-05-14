
namespace Couchbase.IO
{
    /// <summary>
    /// The response status for binary Memcached and Couchbase operations.
    /// </summary>
    public enum ResponseStatus
    {
        /// <summary>
        /// The operation was successful
        /// </summary>
        Success = 0x0000,

        /// <summary>
        /// The key does not exist in the database
        /// </summary>
        KeyNotFound = 0x0001,

        /// <summary>
        /// The key exists in the database.
        /// </summary>
        KeyExists = 0x0002,

        /// <summary>
        /// The value of the object stored was too large.
        /// </summary>
        ValueTooLarge = 0x0003,

        /// <summary>
        /// The arguments of the operation were invalid.
        /// </summary>
        InvalidArguments = 0x0004,

        /// <summary>
        /// The item could be stored in the database
        /// </summary>
        ItemNotStored = 0x0005,

        /// <summary>
        /// The increment operation was called on a non-numeric value
        /// </summary>
        IncrDecrOnNonNumericValue = 0x0006,

        /// <summary>
        /// The VBucket the operation was attempted on, no longer belongs to the server.
        /// <remarks>This is a common during rebalancing after adding or removing a node or during a failover.</remarks>
        /// </summary>
        VBucketBelongsToAnotherServer = 0x0007,

        /// <summary>
        /// The connection to Couchbase could not be authenticated.
        /// </summary>
        /// <remarks>Check the bucket name and/or password being used.</remarks>
        AuthenticationError = 0x0020,

        /// <summary>
        /// During SASL authentication, another step (or more) must be made before authentication is complete.
        /// <remarks>This is a system-level response status.</remarks>
        /// </summary>
        AuthenticationContinue = 0x0021,

        /// <summary>
        /// The value was outside of supported range.
        /// </summary>
        InvalidRange = 0x0022,

        /// <summary>
        /// The server received an unknown command from a client.
        /// </summary>
        UnknownCommand = 0x0081,

        /// <summary>
        /// The server is temporarily out of memory.
        /// </summary>
        OutOfMemory = 0x0082,

        /// <summary>
        /// The operation is not supported.
        /// </summary>
        NotSupported = 0x0083,

        /// <summary>
        /// An internal error has occured.
        /// </summary>
        /// <remarks>See logs for more details.</remarks>
        InternalError = 0x0084,

        /// <summary>
        /// The server was too busy to complete the operation.
        /// </summary>
        Busy = 0x0085,

        /// <summary>
        /// A temporary error has occured in the server.
        /// </summary>
        TemporaryFailure = 0x0086,

        /*The response status's below are not part of the Memcached protocol and represent
         client level failures. They are not supported by all SDKs. */

        /// <summary>
        /// A client error has occured before the operation could be sent to the server.
        /// </summary>
        ClientFailure = 0x0199,

        /// <summary>
        /// The operation exceeded the specified OperationTimeout configured for the client instance.
        /// </summary>
        /// <remarks>The default is 2500ms.</remarks>
        OperationTimeout = 0x0200,

        /// <summary>
        ///  Returned when the client cannot locate a replica within the cluster map config for a replica read.
        ///  This would happen if a bucket was not configured to have replicas; if you encounter this error check
        ///  to make sure you have indeed configured replicas on your bucket.
        /// </summary>
        NoReplicasFound = 0x0300,

        /// <summary>
        /// The node or service that the key has been mapped to is offline or cannot be reached.
        /// </summary>
        NodeUnavailable = 0x0400
    }
}
