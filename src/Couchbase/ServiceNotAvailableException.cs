using System;
using Couchbase.Services;
using Couchbase.Utils;

namespace Couchbase
{
    public class ServiceNotAvailableException : Exception
    {
        public ServiceNotAvailableException(ServiceType serviceType)
            : base($"Service {serviceType.GetDescription()} not available.")
        {

        }
    }
}