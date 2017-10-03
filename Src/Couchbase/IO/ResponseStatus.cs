namespace Couchbase.IO
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
        /// Subdocument error indicating the flag combination for an XATTR operation was invalid.
        /// </summary>
        SubDocXattrInvalidFlagCombo = 0xce,

        /// <summary>
        /// Subdocument error indicating the key combination for an XATTR opeation was invalid.
        /// </summary>
        SubDocXattrInvalidKeyCombo = 0xcf
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
