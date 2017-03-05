using System;
using Couchbase.Core.Serialization;
using Newtonsoft.Json;

namespace Couchbase.IntegrationTests.Utils
{
    /// <summary>
    /// Uses <see cref="DefaultValueHandling.IgnoreAndPopulate"/> when deserializing.
    /// </summary>
    public class IgnoreAndPopulateSerializer : DefaultSerializer
    {
        public IgnoreAndPopulateSerializer()
        {
            DeserializationSettings.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
        }
    }
}
