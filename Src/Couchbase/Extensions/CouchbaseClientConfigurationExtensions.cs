using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Newtonsoft.Json;

namespace Couchbase.Extensions
{
    internal static class CouchbaseClientConfigurationExtensions
    {
        public static string GetConfig(this ICouchbaseClientConfiguration config)
        {
            var converter = new JsonSerializerSettings
            {
                ContractResolver = new InterfaceContractResolver()
            };
            return JsonConvert.SerializeObject(config, converter);
        }

        public static void LogConfig(this ICouchbaseClientConfiguration config, ILog log, Guid clientId)
        {
            if (log.IsInfoEnabled)
            {
                try
                {
                    log.InfoFormat("CID Config: {0} {1}", clientId, GetConfig(config));
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }
        }
    }
}