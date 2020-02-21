using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Diagnostics;
using Xunit;

namespace Couchbase.UnitTests.Diagnostics
{
    public class PingOptionsTests
    {
        [Fact]
        public void Test_ServiceTypes_Defaults_To_All_Services()
        {
            //arrange/act

            var options = new PingOptions();

            //assert

            Assert.Equal(5, options.ServiceTypesValue.Count);
            Assert.Contains(options.ServiceTypesValue, type => type == ServiceType.KeyValue);
            Assert.Contains(options.ServiceTypesValue, type => type == ServiceType.Query);
            Assert.Contains(options.ServiceTypesValue, type => type == ServiceType.Search);
            Assert.Contains(options.ServiceTypesValue, type => type == ServiceType.Analytics);
            Assert.Contains(options.ServiceTypesValue, type => type == ServiceType.KeyValue);
        }

        [Fact]
        public void Can_Override_ServiceType_Defaults()
        {
            //arrange

            var options = new PingOptions();

            //act

            options.ServiceTypes(ServiceType.KeyValue, ServiceType.Query);

            //assert

            Assert.Equal(2, options.ServiceTypesValue.Count);
            Assert.Contains(options.ServiceTypesValue, type => type == ServiceType.KeyValue);
            Assert.Contains(options.ServiceTypesValue, type => type == ServiceType.Query);
        }
    }
}
