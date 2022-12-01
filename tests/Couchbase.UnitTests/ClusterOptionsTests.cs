using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.Retry;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using TraceListener = Couchbase.Core.Diagnostics.Tracing.TraceListener;

namespace Couchbase.UnitTests
{
    public class ClusterOptionsTests
    {
        private readonly ITestOutputHelper _output;
        public ClusterOptionsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void WhenDnsSrvAllowReconfigure()
        {
            var options = new ClusterOptions()
            {
                UserName = "Administrator",
                Password = "password",
                EnableDnsSrvResolution = true
            }.WithConnectionString("dotnet123.cbqeoc.com");

            //fake like DNS SRV records were returned
            var serversOld = new List<HostEndpoint>
            {
                new HostEndpoint("node1-e4c8609a.cbqeoc.com", 11210),
                new HostEndpoint("node2-e4c8609a.cbqeoc.com", 11210),
                new HostEndpoint("node3-e4c8609a.cbqeoc.com", 11210)
            };

            var dnsUri = options.ConnectionStringValue.GetDnsBootStrapUri();

            options.ConnectionStringValue =
                            new ConnectionString(options.ConnectionStringValue, serversOld, true, dnsUri);

            Assert.True(options.ConnectionStringValue.IsValidDnsSrv());
            Assert.Equal(3, options.ConnectionStringValue.Hosts.Count);
            Assert.Equal(new HostEndpoint("node1-e4c8609a.cbqeoc.com", 11210), options.ConnectionStringValue.Hosts[0]);
            Assert.Equal(new HostEndpoint("node2-e4c8609a.cbqeoc.com", 11210), options.ConnectionStringValue.Hosts[1]);
            Assert.Equal(new HostEndpoint("node3-e4c8609a.cbqeoc.com", 11210), options.ConnectionStringValue.Hosts[2]);

            var serversNew = new List<HostEndpoint>
            {
                new HostEndpoint("node1-fd229c81.cbqeoc.com", 11210),
                new HostEndpoint("node2-fd229c81.cbqeoc.com", 11210),
                new HostEndpoint("node3-fd229c81.cbqeoc.com", 11210)
            };

            options.ConnectionStringValue =
                            new ConnectionString(options.ConnectionStringValue, serversNew, true, dnsUri);

            Assert.True(options.ConnectionStringValue.IsValidDnsSrv());
            Assert.Equal(3, options.ConnectionStringValue.Hosts.Count);
            Assert.Equal(new HostEndpoint("node1-fd229c81.cbqeoc.com", 11210), options.ConnectionStringValue.Hosts[0]);
            Assert.Equal(new HostEndpoint("node2-fd229c81.cbqeoc.com", 11210), options.ConnectionStringValue.Hosts[1]);
            Assert.Equal(new HostEndpoint("node3-fd229c81.cbqeoc.com", 11210), options.ConnectionStringValue.Hosts[2]);
        }

        #region KvSendQueueCapacity

        [Fact]
        public void KvSendQueueCapacity_Defaults_To_1024()
        {
            var options = new ClusterOptions();
            Assert.Equal(1024u, options.KvSendQueueCapacity);
        }

        #endregion

        #region ConfigPollInterval

        [Fact]
        public void Test_ConfigPollInterval_Default_Is_2_5Seconds()
        {
            var options = new ClusterOptions();
            Assert.Equal(TimeSpan.FromSeconds(2.5), options.ConfigPollInterval);
        }

        [Fact]
        public void Test_EnableConfigPolling_Default_Is_True()
        {
            var options = new ClusterOptions();
            Assert.True(options.EnableConfigPolling);
        }

        #endregion

        #region EffectiveEnableTls

        [Theory]
        [InlineData(null, null, false)]
        [InlineData(null, "couchbase", false)]
        [InlineData(null, "http", false)]
        [InlineData(null, "couchbases", true)]
        [InlineData(false, null, false)]
        [InlineData(false, "couchbase", false)]
        [InlineData(false, "http", false)]
        [InlineData(false, "couchbases", true)]
        [InlineData(true, null, true)]
        [InlineData(true, "couchbase", true)]
        [InlineData(true, "http", true)]
        [InlineData(true, "couchbases", true)]
        public void EffectiveEnableTls_VariousSources_ExpectedResult(bool? enableTls, string scheme,
            bool expectedResult)
        {
            // Arrange

            var clusterOptions = new ClusterOptions
            {
                EnableTls = enableTls
            };

            if (scheme != null)
            {
                clusterOptions.WithConnectionString($"{scheme}://localhost");
            }

            // Act

            var result = clusterOptions.EffectiveEnableTls;

            // Assert

            Assert.Equal(expectedResult, result);
        }

        #endregion

        #region x509

        [Fact]
        public void X509Certificate_Is_Null_By_Default()
        {
            var options = new ClusterOptions();

            Assert.Null(options.X509CertificateFactory);
        }

        [Fact]
        public void When_Set_X509Certificate_Is_NotNull_And_EnableTls_True()
        {
            var options = new ClusterOptions().
                WithX509CertificateFactory(CertificateFactory.GetCertificatesFromStore(
                new CertificateStoreSearchCriteria
                {
                    FindValue = "value",
                    X509FindType = X509FindType.FindBySubjectName,
                    StoreLocation = StoreLocation.CurrentUser,
                    StoreName = StoreName.CertificateAuthority
                }));

            Assert.NotNull(options.X509CertificateFactory);
            Assert.True(options.EffectiveEnableTls);
        }

        [Fact]
        public void When_X509Certificate_Is_Set_To_Null_Throw_NRE()
        {
            Assert.Throws<NullReferenceException>(() => new ClusterOptions().WithX509CertificateFactory(null));
        }

        #endregion

        #region NetworkResolution

        [Fact]
        public void Test_NetworkConfiguration_Default()
        {
            var options = new ClusterOptions();
            Assert.Equal(NetworkResolution.Auto, options.NetworkResolution);
        }

        [Theory]
        [InlineData(NetworkResolution.External)]
        [InlineData(NetworkResolution.Default)]
        [InlineData(NetworkResolution.Auto)]
        public void Test_NetworkConfiguration_Custom(string networkResolution)
        {
            var options = new ClusterOptions { NetworkResolution = networkResolution };
            Assert.Equal(networkResolution, options.NetworkResolution);
        }

        #endregion

        #region RetryStrategy

        [Fact]
        public void Test_RetryStrategy_Default()
        {
            var clusterOptions = new ClusterOptions();

            Assert.IsType<BestEffortRetryStrategy>(clusterOptions.RetryStrategy);
        }

        [Fact]
        public void When_RetryStrategy_Overridden_Return_FailFastRetryStrategy()
        {
            var myCustomStrategy = new FailFastRetryStrategy();

            var clusterOptions = new ClusterOptions().
                WithRetryStrategy(myCustomStrategy);

            Assert.IsType<FailFastRetryStrategy>(clusterOptions.RetryStrategy);
        }

        #endregion RetryStrategy

        #region Tracing

        [Fact]
        public void When_Tracing_Not_Enabled_Default_To_NoopRequestTracer()
        {
            var options = new ClusterOptions { TracingOptions = { Enabled = false } };
            options.WithThresholdTracing(new ThresholdOptions
            {
                Enabled = false
            }).WithOrphanTracing(options => options.Enabled = false);

            var services = options.BuildServiceProvider();
            var noopRequestTracer = services.GetService(typeof(IRequestTracer));

            Assert.IsAssignableFrom<NoopRequestTracer>(noopRequestTracer);
        }

        [Fact]
        public void When_Tracing_Enabled_Default_To_ThresholdRequestTracer()
        {
            var options = new ClusterOptions();
            options.WithThresholdTracing(new ThresholdOptions
            {
                Enabled = true
            });

            var services = options.BuildServiceProvider();
            var noopRequestTracer = services.GetService(typeof(IRequestTracer));

            Assert.IsAssignableFrom<RequestTracer>(noopRequestTracer);
        }

        [Fact]
        public void When_CustomRequestTracer_Registered_Use_It()
        {
            var options = new ClusterOptions()
            {
                TracingOptions = new TracingOptions
                {
                    RequestTracer = new CustomRequestTracer()
                }
            };
            var services = options.BuildServiceProvider();
            var tracer = services.GetService(typeof(IRequestTracer));

            Assert.IsAssignableFrom<CustomRequestTracer>(tracer);
        }

        public class CustomRequestTracer : IRequestTracer
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
            {
                return new CustomRequestSpan();
            }

            public IRequestTracer Start(TraceListener listener)
            {
                return new CustomRequestTracer();
            }
        }

        public class CustomRequestSpan : IRequestSpan
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IRequestSpan SetAttribute(string key, bool value)
            {
                throw new NotImplementedException();
            }

            public IRequestSpan SetAttribute(string key, string value)
            {
                throw new NotImplementedException();
            }

            public IRequestSpan SetAttribute(string key, uint value)
            {
                throw new NotImplementedException();
            }

            public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
            {
                throw new NotImplementedException();
            }

            public void End()
            {
                throw new NotImplementedException();
            }

            public IRequestSpan? Parent { get; set; }
            public IRequestSpan ChildSpan(string name)
            {
                throw new NotImplementedException();
            }

            public bool CanWrite { get; }
            public string? Id { get; }
            public uint? Duration { get; }
        }

        #endregion

        #region Settings

        [Fact]
        public void UnorderedExecutionEnabled_Defaults_To_True()
        {
            var options = ClusterOptions.Default;

            Assert.True(options.UnorderedExecutionEnabled);
        }

        #endregion
    }
}
