using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.N1QL;

namespace Couchbase.Analytics
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

        public List<Error> Errors { get; set; }
    }
}
