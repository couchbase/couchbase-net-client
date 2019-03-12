using System;

namespace Couchbase.Services.Analytics
{
    public class AnalyticsException : Exception
    {
        public AnalyticsException()
        {
        }

        public AnalyticsException(string message) : base(message)
        {
        }

        public AnalyticsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
