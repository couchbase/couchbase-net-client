using System;
using Couchbase.Utils;

namespace Couchbase
{
    public class ServiceNotAvailableException : CouchbaseException
    {
        public ServiceNotAvailableException(ServiceType serviceType)
            : base($"Service {serviceType.GetDescription()} not available.")
        {
        }
    }
}
