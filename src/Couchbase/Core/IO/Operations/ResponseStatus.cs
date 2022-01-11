namespace Couchbase.Core.IO.Operations
{
      /// <summary>
    /// The response status for binary Memcached and Couchbase operations.
    /// </summary>
    public enum ResponseStatus
    {
        /// <summary>
        ///  Indicates that the enum has not been set.
        /// </summary>
        /// <remarks>0 has already been taken by the Memcached protocol for success.</remarks>
        None = -1,

        /// <summary>
        /// An unknown error occured. Please check logs for more details.
        /// </summary>
        UnknownError = -2,

        /// <summary>
        /// An Error occured and more details can be found in the operation message.
        /// </summary>
        Failure = -3,

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
        /// The item could not be stored in the database
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
        /// Not connected to a bucket.
        /// </summary>
        BucketNotConnected = 0x0008,

        /// <summary>
        /// The requested resource is locked.
        /// </summary>
        Locked = 0x09,

        /// <summary>
        /// The authentication context is stale. You should reauthenticate
        /// </summary>
        AuthStale = 0x1f,

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
        ///  Roll back to an earlier version of the vbucket UUID (_currently_ only used by DCP for agreeing on selecting a starting point)
        /// </summary>
        Rollback = 0x23,

        /// <summary>
        ///  No access (could be opcode, value, bucket etc)
        /// </summary>
        Eaccess = 0x24,

        /// <summary>
        /// The Couchbase cluster is currently initializing this node, and the Cluster manager has not yet granted all users access to the cluster.
        /// </summary>
        NotInitialized = 0x25,

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
        NodeUnavailable = 0x0400,

        /// <summary>
        /// Indicates that a transport layer failure occured while the client was sending or receiving data.
        /// </summary>
        TransportFailure = 0x0500,

        /// <summary>
        /// Document Mutation lost during a hard failover.
        /// </summary>
        DocumentMutationLost = 0x0600,

        /// <summary>
        /// A document mutation was detected on the document being observed.
        /// </summary>
        DocumentMutationDetected = 0x0601,

        /// <summary>
        /// Subdocument error indicating the path inside the JSON is invalid.
        /// </summary>
        SubDocPathNotFound = 0xc0,

        /// <summary>
        /// Subdocument error indicating one of the path components was denoting a wrong type (eg. trying to access
        /// an array index in an entry that isn't an array). Also for arithmetic operations when the value of the
        /// path is not a number.
        /// </summary>
        SubDocPathMismatch = 0xc1,

        /// <summary>
        /// Subdocument error indicating that the path provided is invalid. For operations requiring an array index, this
        /// is returned if the last component of that path isn't an array. Similarly for operations requiring a dictionary,
        /// if the last component isn't a dictionary but eg. an array index.
        /// </summary>
        SubDocPathInvalid = 0xc2,

        /// <summary>
        ///  Subdocument error indicating that the path is too large (ie. the string is too long) or too deep (more that 32 components).
        /// </summary>
        SubDocPathTooBig = 0xc3,

        /// <summary>
        /// Subdocument error indicating that the target document's level of JSON nesting is too deep to be processed by the subdoc service.
        /// </summary>
        SubDocDocTooDeep = 0xc4,

        /// <summary>
        /// Subdocument error indicating that the target document is not flagged or recognized as JSON.
        /// </summary>
        SubDocCannotInsert = 0xc5,

        /// <summary>
        /// Subdocument error indicating that, for arithmetic subdoc operations, the existing number is already too large.
        /// </summary>
        SubDocDocNotJson = 0xc6,

        /// <summary>
        /// Subdocument error indicating that for arithmetic subdoc operations, the operation will make the value too large.
        /// </summary>
        SubDocNumRange = 0xc7,

        /// <summary>
        /// Subdocument error indicating that for arithmetic subdoc operations, the operation will make the value too large.
        /// </summary>
        SubDocDeltaRange = 0xc8,

        /// <summary>
        /// Subdocument error indicating that the last component of the path already exist despite the mutation operation
        /// expecting it not to exist (the mutation was expecting to create only the last part of the path and store the
        /// fragment there).
        /// </summary>
        SubDocPathExists = 0xc9,

        /// <summary>
        /// Subdocument error indicating that, in a multi-specification, an invalid combination of commands were specified,
        /// including the case where too many paths were specified.
        /// </summary>
        SubDocValueTooDeep = 0xca,

        /// <summary>
        ///Subdocument error indicating that, in a multi-specification, an invalid combination of commands were specified,
        ///including the case where too many paths were specified.
        /// </summary>
        SubDocInvalidCombo = 0xcb,

        /// <summary>
        /// Subdocument error indicating that, in a multi-specification, one or more commands failed to execute on a document
        /// which exists (ie. the key was valid).
        /// </summary>
        SubDocMultiPathFailure = 0xcc,

        /// <summary>
        /// SubDocument status indicating that the subdoc operation completed successfully on the deleted document.
        /// </summary>
        SubDocSuccessDeletedDocument = 0xcd,

        /// <summary>
        /// Subdocument error indicating the flag combination for an XATTR operation was invalid.
        /// </summary>
        SubDocXattrInvalidFlagCombo = 0xce,

        /// <summary>
        /// Subdocument error indicating the key combination for an XATTR opeation was invalid.
        /// </summary>
        SubDocXattrInvalidKeyCombo = 0xcf,

        /// <summary>
        /// The server has no knowledge of the requested macro
        /// </summary>
        SubdocXattrUnknownMacro = 0xd0,

        /// <summary>
        /// The server has no knowledge of the requested virtual xattr
        /// </summary>
        SubdocXattrUnknownVattr = 0xd1,

        /// <summary>
        /// Virtual xattrs can't be modified
        /// </summary>
        SubdocXattrCantModifyVattr = 0xd2,

        /// <summary>
        /// [For multi-path commands only] Specified key was found as a Deleted document, but one or more path operations
        /// failed. Examine the individual lookup_result (MULTI_LOOKUP) mutation_result (MULTI_MUTATION) structures for details.
        /// </summary>
        SubdocMultiPathFailureDeleted = 0xd3,

        /// <summary>
        /// According to the spec all xattr commands should come first,
        /// followed by the commands for the document body
        /// </summary>
        SubdocInvalidXattrOrder = 0xd4,

        /// <summary>
        /// Collection does not exist.
        /// </summary>
        UnknownCollection = 0x88,

        /// <summary>
        /// The Scope does not exist.
        /// </summary>
        UnknownScope = 0x8C,

        /// <summary>
        /// Invalid request. Returned if an invalid durability level is specified.
        /// </summary>
        DurabilityInvalidLevel = 0xa0,

        /// <summary>
        /// Valid request, but given durability requirements are impossible to achieve -
        /// because insufficient configured replicas are connected. Assuming level=majority
        /// and C=number of configured nodes, durability becomes impossible if floor((C + 1) / 2)
        /// nodes or greater are offline.
        /// </summary>
        DurabilityImpossible = 0xa1,

        /// <summary>
        /// Returned if an attempt is made to mutate a key which already has a SyncWrite pending.
        /// Transient, the client would typically retry (possibly with backoff). Similar to ELOCKED.
        /// </summary>
        SyncWriteInProgress = 0xa2,

        /// <summary>
        /// The SyncWrite request has not completed in the specified time and has ambiguous result -
        /// it may Succeed or Fail; but the final value is not yet known.
        /// </summary>
        SyncWriteAmbiguous = 0xa3,

        /// <summary>
        /// A SyncWrite request re-commit is in progress.
        /// </summary>
        SyncWriteReCommitInProgress = 0xa4,

        /// <summary>
        /// No collections manifest has been set. The server does not support scopes or collections.
        /// </summary>
        NoCollectionsManifest = 0x89,

        /// <summary>
        /// Rate limited: Network Ingress
        /// </summary>
        RateLimitedNetworkIngress = 0x30,

        /// <summary>
        /// Rate limited: Network Egress
        /// </summary>
        RateLimitedNetworkEgress = 0x31,

        /// <summary>
        /// Rate limited: Max Connections
        /// </summary>
        RateLimitedMaxConnections = 0x32,

        /// <summary>
        /// Rate limited: Max Commands
        /// </summary>
        RateLimitedMaxCommands = 0x33,

        /// <summary>
        /// Quota limited: Max number of scopes has been exceeded
        /// </summary>
        ScopeSizeLimitExceeded = 0x34
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
