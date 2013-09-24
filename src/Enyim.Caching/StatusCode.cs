namespace Enyim
{
    public enum StatusCode
    {
       /*Server Side StatusCodes: http://code.google.com/p/memcached/wiki/BinaryProtocolRevamped#Response_Status */
        NoError = 0x0000,
        KeyNotFound = 0x0001,
        KeyExists = 0x0002,
        ValueTooLarge = 0x0003,
        InvalidArguments = 0x0004,
        ItemNotStored = 0x0005,
        IncrDecrOnNonNumericValue = 0x0006,
        VBucketBelongsToAnotherServer = 0x0007,
        AuthenticationError = 0x0008,
        AuthenticationContinue = 0x0009,
        UnknownCommand = 0x0082,
        OutOfMemory = 0x0082,
        NotSupported = 0x0083,
        InternalError = 0x0084,
        Busy = 0x0085,
        TemporaryFailure = 0x0086,

        /*Client Side StatusCodes*/
        SocketPoolTimeout = 0x091
    }

    public static class StatusCodeExtension
    {
        public static int ToInt(this StatusCode statusCode)
        {
            return (int)statusCode;
        }
    }
}