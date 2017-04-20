using System;

namespace Couchbase.Management
{
    /// <summary>
    /// The number of concurrent readers and writers for the data bucket.
    /// </summary>
    public enum ThreadNumber
    {
        [Obsolete("https://issues.couchbase.com/browse/NCBC-1375")]
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8
    }
}
