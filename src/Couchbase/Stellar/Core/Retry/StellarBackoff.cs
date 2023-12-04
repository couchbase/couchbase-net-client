using System;

namespace Couchbase.Stellar.Core.Retry;

public static class StellarBackoff
{
    public static TimeSpan Exponential(StellarRequest request)
    {
        switch (request.Attempts)
        {
            case 0:
                return TimeSpan.FromMilliseconds(1);
            case 1:
                return TimeSpan.FromMilliseconds(10);
            case 2:
                return TimeSpan.FromMilliseconds(50);
            case 3:
               return TimeSpan.FromMilliseconds(100);
            case 4:
                return TimeSpan.FromMilliseconds(500);
            default:
                return TimeSpan.FromMilliseconds(1000);
        }
    }
}
