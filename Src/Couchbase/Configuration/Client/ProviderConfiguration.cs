using System;

namespace Couchbase.Configuration.Client
{
    public class ProviderConfiguration
    {
        public ProviderConfiguration()
        {
           /* Name = "HttpStreaming";
            TypeName = "Couchbase.Configuration.Server.Providers.Streaming.HttpStreamingProvider, Couchbase";*/
            Name = "CarrierPublication";
            TypeName = "Couchbase.Configuration.Server.Providers.CarrierPublication.CarrierPublicationProvider, Couchbase";
        }
        public string Name { get; set; }

        public string TypeName { get; set; }

        public Type Type { get; set; }
    }
}