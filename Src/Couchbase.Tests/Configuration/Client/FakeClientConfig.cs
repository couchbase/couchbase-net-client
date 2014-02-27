using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;

namespace Couchbase.Tests.Configuration.Client
{
    public class FakeClientConfig : ClientConfiguration
    {
        public string BootstrapPath
        {
            get { return @"Data\\Configuration\\bootstrap.json"; }
        }

        public List<Uri> Servers
        {
            get { throw new NotImplementedException(); }
        }

        public List<ProviderConfiguration> ProviderConfigs { get; set; }
    }
}
