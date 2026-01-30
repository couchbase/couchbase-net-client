using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.Retry;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using TraceListener = Couchbase.Core.Diagnostics.Tracing.TraceListener;

#pragma warning disable CS8632
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
#pragma warning disable CS0618 // Type or member is obsolete
            var options = new ClusterOptions()
            {
                UserName = "Administrator",
                Password = "password",
                EnableDnsSrvResolution = true
            }.WithConnectionString("dotnet123.cbqeoc.com");
#pragma warning restore CS0618 // Type or member is obsolete

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

#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Null(options.X509CertificateFactory);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        public void When_Set_X509Certificate_Is_NotNull_And_EnableTls_True()
        {
#pragma warning disable CS0618 // Type or member is obsolete
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
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.True(options.EffectiveEnableTls);
        }

        [Fact]
        public void When_X509Certificate_Is_Set_To_Null_Throw_NRE()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Throws<NullReferenceException>(() => new ClusterOptions().WithX509CertificateFactory(null));
#pragma warning restore CS0618 // Type or member is obsolete
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

            Assert.IsAssignableFrom<RequestTracerWrapper>(noopRequestTracer);
            Assert.IsAssignableFrom<RequestTracer>(((RequestTracerWrapper)noopRequestTracer).InnerTracer);
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

            Assert.IsAssignableFrom<RequestTracerWrapper>(tracer);
            Assert.IsAssignableFrom<CustomRequestTracer>(((RequestTracerWrapper)tracer).InnerTracer);
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

        #region BuildServiceProvider

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BuildServiceProvider_WithoutCustomMeter_Succeeds(bool loggingMeterEnabled)
        {
            // Arrange

            var options = new ClusterOptions()
                .WithLoggingMeterOptions(options => options.Enabled(loggingMeterEnabled));

            // Act

            var provider = options.BuildServiceProvider();

            // Assert

            var meter = provider.GetRequiredService<IMeter>();
            Assert.NotNull(meter);

            if (loggingMeterEnabled)
            {
                Assert.IsType<LoggingMeter>(meter);
            }
            else
            {
                Assert.IsType<NoopMeter>(meter);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BuildServiceProvider_WithCustomMeter_UsesCustomMeter(bool loggingMeterEnabled)
        {
            // Arrange

            var options = new ClusterOptions()
                .WithLoggingMeterOptions(options => options.Enabled(loggingMeterEnabled))
                .AddClusterService<IMeter>(new MockMeter());

            // Act

            var provider = options.BuildServiceProvider();

            // Assert

            var meter = provider.GetRequiredService<IMeter>();
            Assert.NotNull(meter);
            Assert.IsType<MockMeter>(meter);
        }

        #endregion

        #region Authenticators

        #region WithPasswordAuthentication

        [Fact]
        public void WithPasswordAuthentication_SetsPasswordAuthenticator()
        {
            var options = new ClusterOptions();

            options.WithPasswordAuthentication("testUser", "testPassword");

            var authenticator = options.Authenticator;
            Assert.NotNull(authenticator);
            Assert.IsType<PasswordAuthenticator>(authenticator);
        }

        [Fact]
        public void WithPasswordAuthentication_SetsCorrectCredentials()
        {
            var options = new ClusterOptions();

            options.WithPasswordAuthentication("myUsername", "myPassword");

            var authenticator = options.Authenticator as PasswordAuthenticator;
            Assert.NotNull(authenticator);
            Assert.Equal("myUsername", authenticator.Username);
            Assert.Equal("myPassword", authenticator.Password);
        }

        [Fact]
        public void WithPasswordAuthentication_SupportsBothTlsAndNonTls()
        {
            var options = new ClusterOptions();

            options.WithPasswordAuthentication("user", "pass");

            var authenticator = options.Authenticator;
            Assert.NotNull(authenticator);
            Assert.True(authenticator.SupportsTls);
            Assert.True(authenticator.SupportsNonTls);
        }

        [Fact]
        public void WithPasswordAuthentication_ReturnsNullClientCertificates()
        {
            var options = new ClusterOptions();
            options.WithPasswordAuthentication("user", "pass");

            var authenticator = options.Authenticator;
            Assert.NotNull(authenticator);
            var certs = authenticator.GetClientCertificates();
            Assert.Null(certs);
        }

        #endregion

        #region WithCertificateAuthentication

        [Fact]
        public void WithCertificateAuthentication_SetsCertificateAuthenticator()
        {
            var options = new ClusterOptions();
            var mockFactory = new Mock<ICertificateFactory>();

            options.WithCertificateAuthentication(mockFactory.Object);

            var authenticator = options.Authenticator;
            Assert.NotNull(authenticator);
            Assert.IsType<CertificateAuthenticator>(authenticator);
        }

        [Fact]
        public void WithCertificateAuthentication_EnablesTls()
        {
            var options = new ClusterOptions();
            var mockFactory = new Mock<ICertificateFactory>();

            options.WithCertificateAuthentication(mockFactory.Object);

            Assert.True(options.EnableTls);
        }

        [Fact]
        public void WithCertificateAuthentication_UsesCertificateFactory()
        {
            var options = new ClusterOptions();
            var mockFactory = new Mock<ICertificateFactory>();
            var expectedCerts = new X509Certificate2Collection();
            mockFactory.Setup(f => f.GetCertificates()).Returns(expectedCerts);

            options.WithCertificateAuthentication(mockFactory.Object);

            var authenticator = options.Authenticator as CertificateAuthenticator;
            Assert.NotNull(authenticator);
            Assert.Same(mockFactory.Object, authenticator.CertificateFactory);
        }

        [Fact]
        public void WithCertificateAuthentication_SupportsTlsOnly()
        {
            var options = new ClusterOptions();
            var mockFactory = new Mock<ICertificateFactory>();

            options.WithCertificateAuthentication(mockFactory.Object);

            var authenticator = options.Authenticator;
            Assert.NotNull(authenticator);
            Assert.True(authenticator.SupportsTls);
            Assert.False(authenticator.SupportsNonTls);
        }

        [Fact]
        public void WithCertificateAuthentication_ReturnsClientCertificatesFromFactory()
        {
            var options = new ClusterOptions();
            var mockFactory = new Mock<ICertificateFactory>();
            var expectedCerts = new X509Certificate2Collection();
            mockFactory.Setup(f => f.GetCertificates()).Returns(expectedCerts);

            options.WithCertificateAuthentication(mockFactory.Object);

            var authenticator = options.Authenticator;
            Assert.NotNull(authenticator);
            var certs = authenticator.GetClientCertificates();
            Assert.Same(expectedCerts, certs);
        }

        #endregion

        #region WithAuthenticator

        [Fact]
        public void WithAuthenticator_SetsCustomAuthenticator()
        {
            var options = new ClusterOptions();
            var mockAuthenticator = new Mock<IAuthenticator>();

            options.WithAuthenticator(mockAuthenticator.Object);

            Assert.Same(mockAuthenticator.Object, options.Authenticator);
        }

        [Fact]
        public void WithAuthenticator_ThrowsArgumentNullException_WhenAuthenticatorIsNull()
        {
            var options = new ClusterOptions();

            Assert.Throws<ArgumentNullException>(() => options.WithAuthenticator(null!));
        }

        #endregion

        #region GetEffectiveAuthenticator

        [Fact]
        public void GetEffectiveAuthenticator_ReturnsSetAuthenticator_WhenAuthenticatorIsSet()
        {
            var options = new ClusterOptions();
            var mockAuthenticator = new Mock<IAuthenticator>();
            options.WithAuthenticator(mockAuthenticator.Object);

            var result = options.GetEffectiveAuthenticator();

            Assert.Same(mockAuthenticator.Object, result);
        }

        [Fact]
        public void GetEffectiveAuthenticator_PrefersExplicitAuthenticator_OverLegacyUserName()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var options = new ClusterOptions
            {
                UserName = "legacyUser",
                Password = "legacyPass"
            };
#pragma warning restore CS0618 // Type or member is obsolete
            var mockAuthenticator = new Mock<IAuthenticator>();
            options.WithAuthenticator(mockAuthenticator.Object);

            var result = options.GetEffectiveAuthenticator();

            Assert.Same(mockAuthenticator.Object, result);
        }

        [Fact]
        public void GetEffectiveAuthenticator_FallsBackToLegacyX509CertificateFactory()
        {
            var options = new ClusterOptions();
            var mockFactory = new Mock<ICertificateFactory>();
#pragma warning disable CS0618 // Type or member is obsolete
            options.X509CertificateFactory = mockFactory.Object;
#pragma warning restore CS0618 // Type or member is obsolete

            var result = options.GetEffectiveAuthenticator();

            Assert.NotNull(result);
            Assert.IsType<CertificateAuthenticator>(result);
            Assert.Same(mockFactory.Object, ((CertificateAuthenticator)result).CertificateFactory);
        }

        [Fact]
        public void GetEffectiveAuthenticator_FallsBackToLegacyUserNameAndPassword()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var options = new ClusterOptions
            {
                UserName = "legacyUser",
                Password = "legacyPass"
            };
#pragma warning restore CS0618 // Type or member is obsolete

            var result = options.GetEffectiveAuthenticator();

            Assert.NotNull(result);
            Assert.IsType<PasswordAuthenticator>(result);
            var passwordAuth = (PasswordAuthenticator)result;
            Assert.Equal("legacyUser", passwordAuth.Username);
            Assert.Equal("legacyPass", passwordAuth.Password);
        }

        [Fact]
        public void GetEffectiveAuthenticator_FallsBackToLegacyUserName_WithEmptyPassword()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var options = new ClusterOptions
            {
                UserName = "legacyUser"
                // Password is null
            };
#pragma warning restore CS0618 // Type or member is obsolete

            var result = options.GetEffectiveAuthenticator();

            Assert.NotNull(result);
            Assert.IsType<PasswordAuthenticator>(result);
            var passwordAuth = (PasswordAuthenticator)result;
            Assert.Equal("legacyUser", passwordAuth.Username);
            Assert.Equal(string.Empty, passwordAuth.Password);
        }

        [Fact]
        public void GetEffectiveAuthenticator_ThrowsInvalidConfigurationException_WhenNoAuthenticatorConfigured()
        {
            var options = new ClusterOptions();

            var ex = Assert.Throws<InvalidConfigurationException>(() => options.GetEffectiveAuthenticator());
            Assert.Contains("No authentication method is configured", ex.Message);
        }

        [Fact]
        public void GetEffectiveAuthenticator_CachesAuthenticator_OnSubsequentCalls()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var options = new ClusterOptions
            {
                UserName = "user",
                Password = "pass"
            };
#pragma warning restore CS0618 // Type or member is obsolete

            var result1 = options.GetEffectiveAuthenticator();
            var result2 = options.GetEffectiveAuthenticator();

            Assert.Same(result1, result2);
        }

        [Fact]
        public void GetEffectiveAuthenticator_PrefersX509Factory_OverUserNamePassword()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var options = new ClusterOptions
            {
                UserName = "user",
                Password = "pass"
            };
            var mockFactory = new Mock<ICertificateFactory>();
            options.X509CertificateFactory = mockFactory.Object;
#pragma warning restore CS0618 // Type or member is obsolete

            var result = options.GetEffectiveAuthenticator();

            Assert.IsType<CertificateAuthenticator>(result);
        }

        [Fact]
        public void GetEffectiveAuthenticator_UsesEffectiveEnableTls_ForPasswordAuthenticator()
        {
            var options = new ClusterOptions()
                .WithConnectionString("couchbases://localhost");
#pragma warning disable CS0618 // Type or member is obsolete
            options.UserName = "user";
            options.Password = "pass";
#pragma warning restore CS0618 // Type or member is obsolete

            var authenticator = options.GetEffectiveAuthenticator();

            Assert.IsType<PasswordAuthenticator>(authenticator);
        }

        #endregion

        #region Authenticator Overwriting

        [Fact]
        public void SettingNewAuthenticator_OverwritesPreviousAuthenticator()
        {
            var options = new ClusterOptions();
            options.WithPasswordAuthentication("user1", "pass1");

            var mockFactory = new Mock<ICertificateFactory>();
            options.WithCertificateAuthentication(mockFactory.Object);

            var authenticator = options.Authenticator;
            Assert.IsType<CertificateAuthenticator>(authenticator);
        }

        [Fact]
        public void WithPasswordAuthentication_OverwritesCertificateAuthentication()
        {
            var options = new ClusterOptions();
            var mockFactory = new Mock<ICertificateFactory>();
            options.WithCertificateAuthentication(mockFactory.Object);

            options.WithPasswordAuthentication("newUser", "newPass");

            var authenticator = options.Authenticator as PasswordAuthenticator;
            Assert.NotNull(authenticator);
            Assert.Equal("newUser", authenticator.Username);
        }

        #endregion

        #endregion

        #region Helpers

        // ReSharper disable once ClassNeverInstantiated.Local
        private class MockMeter : IMeter
        {
            public IValueRecorder ValueRecorder(string name, IDictionary<string, string> tags)
            {
                return NoopValueRecorder.Instance;
            }

            public void Dispose()
            {
            }
        }

        #endregion
    }
}
