using System;

namespace Couchbase.N1QL
{
    [Obsolete("The POST + JSON method is now always used", true)]
    public enum Method
    {
        None,
        Get,
        Post,
        Json
    }
}
