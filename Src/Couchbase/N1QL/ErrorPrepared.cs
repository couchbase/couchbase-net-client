namespace Couchbase
{
    /// <summary>
    /// An enum listing the N1QL codes that should trigger a retry for non adhoc queries.
    /// </summary>
    /// <remarks>Generic (5000) also needs a check of the message content to determine if
    /// retry is applicable or not</remarks>
    internal enum ErrorPrepared
    {
        Unrecognized = 4050,
        UnableToDecode = 4070,
        Generic = 5000
    }
}

