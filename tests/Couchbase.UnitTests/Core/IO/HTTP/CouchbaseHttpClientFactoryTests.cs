using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.HTTP;

public class CouchbaseHttpClientFactoryTests
{
    private readonly Mock<ILogger<CouchbaseHttpClientFactory>> _mockLogger;
    private readonly Mock<IRedactor> _mockRedactor;
    private readonly Mock<ICertificateValidationCallbackFactory> _mockCallbackFactory;

    public CouchbaseHttpClientFactoryTests()
    {
        _mockLogger = new Mock<ILogger<CouchbaseHttpClientFactory>>();
        _mockRedactor = new Mock<IRedactor>();
        _mockCallbackFactory = new Mock<ICertificateValidationCallbackFactory>();

        // Setup callback factory to return a valid callback
        _mockCallbackFactory
            .Setup(x => x.CreateForHttp())
            .Returns((sender, certificate, chain, errors) => true);
        _mockCallbackFactory
            .Setup(x => x.CreateForKv())
            .Returns((sender, certificate, chain, errors) => true);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesHandler_WithCurrentAuthenticator()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithPasswordAuthentication("user", "password")
            .WithConnectionString("couchbase://localhost");

        using var context = new ClusterContext(null, clusterOptions);

        // Act
        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        // Assert
        Assert.NotNull(factory._sharedHandler);
    }

    #endregion

    #region Handler Recreation with CertificateAuthenticator Tests

    [Fact]
    public void Create_WhenAuthenticatorChangesToNewCertificateAuthenticator_RecreatesHandler()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");

        var initialCertFactory = CreateMockCertificateFactory();
        var initialCertAuth = new CertificateAuthenticator(initialCertFactory.Object);
        clusterOptions.Authenticator = initialCertAuth;

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        var initialHandler = factory._sharedHandler;

        // Create a new CertificateAuthenticator
        var newCertFactory = CreateMockCertificateFactory();
        var newCertAuth = new CertificateAuthenticator(newCertFactory.Object);

        // Act - Change the authenticator
        clusterOptions.Authenticator = newCertAuth;
        using var client = factory.Create();

        // Assert
        Assert.NotSame(initialHandler, factory._sharedHandler);
    }

    [Fact]
    public void Create_WhenSameCertificateAuthenticator_DoesNotRecreateHandler()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");

        var certFactory = CreateMockCertificateFactory();
        var certAuth = new CertificateAuthenticator(certFactory.Object);
        clusterOptions.Authenticator = certAuth;

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        var initialHandler = factory._sharedHandler;

        // Act - Call Create multiple times without changing authenticator
        using var client1 = factory.Create();
        using var client2 = factory.Create();

        // Assert
        Assert.Same(initialHandler, factory._sharedHandler);
    }

    [Fact]
    public void Create_WhenPasswordAuthenticatorChanges_DoesNotRecreateHandler()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithPasswordAuthentication("user1", "password1")
            .WithConnectionString("couchbase://localhost");

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        var initialHandler = factory._sharedHandler;

        // Act - Change to a different PasswordAuthenticator
        clusterOptions.Authenticator = new PasswordAuthenticator("user2", "password2");
        using var client = factory.Create();

        // Assert - Handler should NOT be recreated for non-certificate authenticators
        Assert.Same(initialHandler, factory._sharedHandler);
    }

    [Fact]
    public void Create_WhenChangingFromPasswordToCertificateAuthenticator_RecreatesHandler()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithPasswordAuthentication("user", "password")
            .WithConnectionString("couchbases://localhost");

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        var initialHandler = factory._sharedHandler;

        // Act - Change to a CertificateAuthenticator
        var certFactory = CreateMockCertificateFactory();
        var certAuth = new CertificateAuthenticator(certFactory.Object);
        clusterOptions.Authenticator = certAuth;
        using var client = factory.Create();

        // Assert
        Assert.NotSame(initialHandler, factory._sharedHandler);
    }

    #endregion

    #region Rotating Certificate Factory Tests

    [Fact]
    public void Create_WhenRotatingCertificateFactoryHasUpdates_RecreatesHandler()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");

        var mockRotatingFactory = new Mock<IRotatingCertificateFactory>();
        mockRotatingFactory
            .Setup(x => x.GetCertificates())
            .Returns(CreateTestCertificateCollection());
        mockRotatingFactory
            .SetupSequence(x => x.HasUpdates)
            .Returns(false)  // First Create() call: no updates
            .Returns(true)  // Second Create() call: has updates
            .Returns(true); // Need to add it again since ShouldRecreateHandler() is called twice

        var certAuth = new CertificateAuthenticator(mockRotatingFactory.Object);
        clusterOptions.Authenticator = certAuth;

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        var initialHandler = factory._sharedHandler;

        // Act - First Create (no updates)
        using var client1 = factory.Create();
        var handlerAfterFirstCreate = factory._sharedHandler;

        // Second Create (has updates)
        using var client2 = factory.Create();

        // Assert
        Assert.Same(initialHandler, handlerAfterFirstCreate);
        Assert.NotSame(initialHandler, factory._sharedHandler);
    }

    [Fact]
    public void Create_WhenRotatingCertificateFactoryHasNoUpdates_DoesNotRecreateHandler()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");

        var mockRotatingFactory = new Mock<IRotatingCertificateFactory>();
        mockRotatingFactory
            .Setup(x => x.GetCertificates())
            .Returns(CreateTestCertificateCollection());
        mockRotatingFactory
            .Setup(x => x.HasUpdates)
            .Returns(false);

        var certAuth = new CertificateAuthenticator(mockRotatingFactory.Object);
        clusterOptions.Authenticator = certAuth;

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        var initialHandler = factory._sharedHandler;

        // Act
        using var client1 = factory.Create();
        using var client2 = factory.Create();
        using var client3 = factory.Create();

        // Assert
        Assert.Same(initialHandler, factory._sharedHandler);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Create_ConcurrentCallsWithAuthenticatorChange_IsThreadSafe()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");

        var certFactory1 = CreateMockCertificateFactory();
        var certAuth1 = new CertificateAuthenticator(certFactory1.Object);
        clusterOptions.Authenticator = certAuth1;

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        var barrier = new Barrier(10);
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act - Simulate concurrent calls with authenticator changes
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            var taskIndex = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    barrier.SignalAndWait();

                    // Some tasks change the authenticator
                    if (taskIndex % 2 == 0)
                    {
                        var newCertFactory = CreateMockCertificateFactory();
                        var newCertAuth = new CertificateAuthenticator(newCertFactory.Object);
                        clusterOptions.Authenticator = newCertAuth;
                    }

                    // All tasks call Create
                    using var client = factory.Create();
                    Assert.NotNull(client);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert - No exceptions should have been thrown
        Assert.Empty(exceptions);
        Assert.NotNull(factory._sharedHandler);
    }

    [Fact]
    public async Task Create_RapidConcurrentCalls_DoesNotThrow()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");

        var certFactory = CreateMockCertificateFactory();
        var certAuth = new CertificateAuthenticator(certFactory.Object);
        clusterOptions.Authenticator = certAuth;

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        // Act - Many concurrent Create calls
        var tasks = new Task[100];
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                using var client = factory.Create();
                Assert.NotNull(client);
            });
        }

        // Assert - Should complete without exceptions
        await Task.WhenAll(tasks);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_ReturnsValidHttpClient()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithPasswordAuthentication("user", "password")
            .WithConnectionString("couchbase://localhost");

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        // Act
        using var client = factory.Create();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<HttpClient>(client);
    }

    [Fact]
    public void Create_MultipleAuthenticatorChanges_RecreatesHandlerEachTime()
    {
        // Arrange
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");

        var certFactory1 = CreateMockCertificateFactory();
        var certAuth1 = new CertificateAuthenticator(certFactory1.Object);
        clusterOptions.Authenticator = certAuth1;

        using var context = new ClusterContext(null, clusterOptions);

        var factory = new CouchbaseHttpClientFactory(
            context,
            _mockLogger.Object,
            _mockRedactor.Object,
            _mockCallbackFactory.Object);

        var handler1 = factory._sharedHandler;

        // Act & Assert - First change
        var certFactory2 = CreateMockCertificateFactory();
        var certAuth2 = new CertificateAuthenticator(certFactory2.Object);
        clusterOptions.Authenticator = certAuth2;
        using var client1 = factory.Create();
        var handler2 = factory._sharedHandler;
        Assert.NotSame(handler1, handler2);

        // Second change
        var certFactory3 = CreateMockCertificateFactory();
        var certAuth3 = new CertificateAuthenticator(certFactory3.Object);
        clusterOptions.Authenticator = certAuth3;
        using var client2 = factory.Create();
        var handler3 = factory._sharedHandler;
        Assert.NotSame(handler2, handler3);

        // Third change
        var certFactory4 = CreateMockCertificateFactory();
        var certAuth4 = new CertificateAuthenticator(certFactory4.Object);
        clusterOptions.Authenticator = certAuth4;
        using var client3 = factory.Create();
        var handler4 = factory._sharedHandler;
        Assert.NotSame(handler3, handler4);
    }

    #endregion

    #region Helper Methods

    private Mock<ICertificateFactory> CreateMockCertificateFactory()
    {
        var mockFactory = new Mock<ICertificateFactory>();
        mockFactory
            .Setup(x => x.GetCertificates())
            .Returns(CreateTestCertificateCollection());
        return mockFactory;
    }

    private static X509Certificate2Collection CreateTestCertificateCollection()
    {
        var collection = new X509Certificate2Collection();
        var cert = CreateSelfSignedCertificate("TestCert", DateTime.UtcNow.AddDays(30));
        collection.Add(cert);
        return collection;
    }

    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName, DateTime notAfter)
    {
        var distinguishedName = new X500DistinguishedName($"CN={subjectName}");

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            distinguishedName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(DateTime.UtcNow.AddDays(-1), notAfter);
        return certificate;
    }

    #endregion
}
