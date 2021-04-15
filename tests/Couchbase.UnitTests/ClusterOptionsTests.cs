using System;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.Retry;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ClusterOptionsTests
    {

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
            var options =  new ClusterOptions();
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
        [InlineData(false, "couchbases", false)]
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
           Assert.Throws<NullReferenceException>(()=> new ClusterOptions().WithX509CertificateFactory(null));
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
            var options = new ClusterOptions {NetworkResolution = networkResolution};
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
            var options = new ClusterOptions();
            options.WithThresholdTracing(new ThresholdOptions
            {
                Enabled = false
            });

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
                Enabled = true,
                RequestTracer = new ThresholdLoggingTracer(new ThresholdOptions(), new LoggerFactory())
            });

            var services = options.BuildServiceProvider();
            var noopRequestTracer = services.GetService(typeof(IRequestTracer));

            Assert.IsAssignableFrom<ThresholdLoggingTracer>(noopRequestTracer);
        }

        [Fact]
        public void When_Tracing_Enabled_Custom_To_CustomRequestTracer()
        {
            var options = new ClusterOptions();
            options.WithThresholdTracing(new ThresholdOptions
            {
                Enabled = true,
                RequestTracer = new CustomRequestTracer()
        });

            var services = options.BuildServiceProvider();
            var noopRequestTracer = services.GetService(typeof(IRequestTracer));

            Assert.IsAssignableFrom<CustomRequestTracer>(noopRequestTracer);
        }

        [Fact]
        public void When_Tracing_Disabled_Custom_To_NoopRequestTracer()
        {
            var options = new ClusterOptions();
            options.WithThresholdTracing(new ThresholdOptions
            {
                Enabled = false
            });
            options.RequestTracer = new CustomRequestTracer();

            var services = options.BuildServiceProvider();
            var noopRequestTracer = services.GetService(typeof(IRequestTracer));

            Assert.IsAssignableFrom<NoopRequestTracer>(noopRequestTracer);
        }

        public class CustomRequestTracer : IRequestTracer
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
            {
                throw new NotImplementedException();
            }

            public IRequestTracer Start(TraceListener listener)
            {
                throw new NotImplementedException();
            }
        }

        #endregion
    }
}
