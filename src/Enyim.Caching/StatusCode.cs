namespace Enyim
{
    public enum StatusCode
    {
        /*Server Side StatusCodes: https://github.com/couchbase/libcouchbase/blob/bb1a3edfd89aa0c1cc3e0417db5341706aaa117e/include/memcached/protocol_binary.h*/
        Success = 0x0000,
        KeyNotFound = 0x0001,
        KeyExists = 0x0002,
        ValueTooLarge = 0x0003,
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

        /*Client Side StatusCodes*/
        SocketPoolTimeout = 0x091,
		UnableToLocateNode = 0x092
    }

    public static class StatusCodeExtension
    {
        public static int ToInt(this StatusCode statusCode)
        {
            return (int)statusCode;
        }
    }
}