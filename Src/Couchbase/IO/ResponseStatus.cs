
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


        ItemNotStored = 0x0005,
        IncrDecrOnNonNumericValue = 0x0006,
        VBucketBelongsToAnotherServer = 0x0007,
        AuthenticationError = 0x0020,
        AuthenticationContinue = 0x0021,
        InvalidRange = 0x0022,
        UnknownCommand = 0x0081,
        OutOfMemory = 0x0082,
        NotSupported = 0x0083,
        InternalError = 0x0084,
        Busy = 0x0085,
        TemporaryFailure = 0x0086,
    }
}
