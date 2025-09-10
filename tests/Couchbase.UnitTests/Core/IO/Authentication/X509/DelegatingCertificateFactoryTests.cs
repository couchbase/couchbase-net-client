using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Authentication.X509
{
    public class DelegatingCertificateFactoryTests
    {
        private readonly Mock<ICertificateFactory> _mockCertificateFactory;

        public DelegatingCertificateFactoryTests()
        {
            _mockCertificateFactory = new Mock<ICertificateFactory>();
        }

        [Fact]
        public void Constructor_WithValidCertificateFactory_ShouldCreateInstance()
        {
            // Arrange & Act
            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Assert
            Assert.NotNull(factory);
            Assert.False(factory.HasUpdates); // Should be false initially
        }

        [Fact]
        public void Constructor_WithNullCertificateFactory_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DelegatingCertificateFactory(null!));
        }

        [Fact]
        public void GetCertificates_FirstCall_ShouldCallDelegateAndSetHasUpdates()
        {
            // Arrange
            var certificates = CreateTestCertificateCollection(2);
            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(certificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Act
            var result = factory.GetCertificates();

            // Assert
            Assert.Same(certificates, result);
            Assert.True(factory.HasUpdates);
            _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.Once);
        }

        [Fact]
        public void GetCertificates_SubsequentCalls_ShouldReturnCachedCertificates()
        {
            // Arrange
            var certificates = CreateTestCertificateCollection(1);
            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(certificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Act - First call
            var result1 = factory.GetCertificates();
            // Act - Second call
            var result2 = factory.GetCertificates();
            // Act - Third call
            var result3 = factory.GetCertificates();

            // Assert
            Assert.Same(certificates, result1);
            Assert.Same(result1, result2);
            Assert.Same(result2, result3);
            Assert.True(factory.HasUpdates);
            // Should only call the delegate once (for the first call)
            _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.Once);
        }

        [Fact]
        public void GetCertificates_WithEmptyCollection_ShouldReturnEmptyCollectionAndSetHasUpdates()
        {
            // Arrange
            var emptyCertificates = new X509Certificate2Collection();
            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(emptyCertificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Act
            var result = factory.GetCertificates();

            // Assert
            Assert.Same(emptyCertificates, result);
            Assert.Empty(result);
            Assert.True(factory.HasUpdates);
        }

        [Fact]
        public void SetDelegatingCertificate_ShouldUpdateUnderlyingFactory()
        {
            // Arrange
            var originalCertificates = CreateTestCertificateCollection(1);
            var newCertificates = CreateTestCertificateCollection(2);

            var newMockFactory = new Mock<ICertificateFactory>();
            newMockFactory.Setup(x => x.GetCertificates()).Returns(newCertificates);

            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(originalCertificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Act
            factory.SetDelegatingCertificate(newMockFactory.Object);

            // Clear cache by getting certificates to trigger new factory call
            var result = factory.GetCertificates();

            // Assert - should get certificates from new factory
            Assert.Same(newCertificates, result);
            newMockFactory.Verify(x => x.GetCertificates(), Times.Once);
        }

        [Fact]
        public void SetDelegatingCertificate_WithNullFactory_ShouldThrowArgumentNullException()
        {
            // Arrange
            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.SetDelegatingCertificate(null!));
        }

        [Fact]
        public void RefreshClientHandler_WithValidNewCertificates_ShouldUpdateCacheAndSetHasUpdates()
        {
            // Arrange
            var initialCertificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(10));
            var newCertificates = CreateTestCertificateCollection(2, DateTime.UtcNow.AddDays(20));

            _mockCertificateFactory.SetupSequence(x => x.GetCertificates())
                                  .Returns(initialCertificates)
                                  .Returns(newCertificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Initialize cache
            factory.GetCertificates();

            var expiresIn = TimeSpan.FromDays(5);

            // Act
            factory.RefreshClientHandler(expiresIn);

            // Assert
            Assert.True(factory.HasUpdates);
            _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.Exactly(2));
        }

        [Fact]
        public void RefreshClientHandler_WithExpiredCertificates_ShouldNotUpdateCache()
        {
            // Arrange
            var initialCertificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(10));
            var expiredCertificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(1)); // Expires in 1 day

            _mockCertificateFactory.SetupSequence(x => x.GetCertificates())
                                  .Returns(initialCertificates)
                                  .Returns(expiredCertificates)
                                  .Returns(expiredCertificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Initialize cache
            var initialResult = factory.GetCertificates();

            // Reset HasUpdates flag by calling RefreshClientHandler once
            factory.RefreshClientHandler(TimeSpan.FromDays(5));

            var expiresIn = TimeSpan.FromDays(5); // Require at least 5 days before expiry

            // Act
            factory.RefreshClientHandler(expiresIn);

            // Assert - HasUpdates should be false because expired certificates were not added
            Assert.False(factory.HasUpdates);
        }

        [Fact]
        public void RefreshClientHandler_WithDuplicateCertificates_ShouldNotUpdateCache()
        {
            // Arrange
            var certificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(20));

            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(certificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Initialize cache
            factory.GetCertificates();

            var expiresIn = TimeSpan.FromDays(5);

            // Act
            factory.RefreshClientHandler(expiresIn);

            // Assert - HasUpdates should be false because no new certificates were found
            Assert.False(factory.HasUpdates);
        }

        [Fact]
        public void RefreshClientHandler_ResetsHasUpdatesFlag()
        {
            // Arrange
            var certificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(20));
            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(certificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Initialize cache and set HasUpdates to true
            factory.GetCertificates();
            Assert.True(factory.HasUpdates);

            var expiresIn = TimeSpan.FromDays(5);

            // Act
            factory.RefreshClientHandler(expiresIn);

            // Assert - HasUpdates should be reset to false since no new certificates were added
            Assert.False(factory.HasUpdates);
        }

        [Fact]
        public void DelegatingCertificateFactory_ImplementsIRotatingCertificateFactory()
        {
            // Arrange & Act
            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Assert
            Assert.IsAssignableFrom<IRotatingCertificateFactory>(factory);
            Assert.IsAssignableFrom<ICertificateFactory>(factory);
        }

        [Fact]
        public void GetCertificates_ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var certificates = CreateTestCertificateCollection(3);
            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(certificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);
            const int threadCount = 10;
            const int iterationsPerThread = 100;
            var results = new X509Certificate2Collection[threadCount * iterationsPerThread];
            var exceptions = new Exception[threadCount];

            // Act
            var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        results[threadIndex * iterationsPerThread + i] = factory.GetCertificates();
                    }
                }
                catch (Exception ex)
                {
                    exceptions[threadIndex] = ex;
                }
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert
            Assert.All(exceptions, ex => Assert.Null(ex));
            Assert.All(results, result => Assert.Same(certificates, result));
            // Should only call the underlying factory once despite multiple threads
            _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.Once);
        }

        [Fact]
        public void RefreshClientHandler_ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var initialCertificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(10));
            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(initialCertificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Initialize cache
            factory.GetCertificates();

            const int threadCount = 5;
            var exceptions = new Exception[threadCount];
            var expiresIn = TimeSpan.FromDays(5);

            // Act
            var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
            {
                try
                {
                    factory.RefreshClientHandler(expiresIn);
                }
                catch (Exception ex)
                {
                    exceptions[threadIndex] = ex;
                }
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert
            Assert.All(exceptions, ex => Assert.Null(ex));
        }

        [Fact]
        public void HasUpdates_InitialState_ShouldBeFalse()
        {
            // Arrange & Act
            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Assert
            Assert.False(factory.HasUpdates);
        }

        [Fact]
        public void HasUpdates_AfterGetCertificates_ShouldBeTrue()
        {
            // Arrange
            var certificates = CreateTestCertificateCollection(1);
            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(certificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Act
            factory.GetCertificates();

            // Assert
            Assert.True(factory.HasUpdates);
        }

        [Fact]
        public async Task Test_Integrated_Timer_Calls_GetCertificates()
        {
            // Arrange
            var originalCertificates = new X509Certificate2Collection();
            var newCertificates = CreateTestCertificateCollection(1);

            var newMockFactory = new Mock<ICertificateFactory>();
            newMockFactory.Setup(x => x.GetCertificates()).Returns(newCertificates);

            _mockCertificateFactory.Setup(x => x.GetCertificates()).Returns(originalCertificates);

            var factory = new DelegatingCertificateFactory(_mockCertificateFactory.Object);

            // Assert initial state
            Assert.False(factory.HasUpdates);

            var clusterOptions = new ClusterOptions().WithX509CertificateFactory(factory);

            var expiresIn = TimeSpan.FromMilliseconds(2);
            using (var timer = new Timer(factory.RefreshClientHandler, expiresIn, TimeSpan.Zero,
                       TimeSpan.FromMilliseconds(10)))
            {
                await Task.Delay(15);

                // Act - call GetCertificates to set HasUpdates to true
                factory.GetCertificates();
                Assert.True(factory.HasUpdates);
            }
        }

        private static X509Certificate2Collection CreateTestCertificateCollection(int count, DateTime? notAfter = null)
        {
            var collection = new X509Certificate2Collection();
            var expiryDate = notAfter ?? DateTime.UtcNow.AddDays(30);

            for (int i = 0; i < count; i++)
            {
                // Create a self-signed certificate for testing
                var cert = CreateSelfSignedCertificate($"TestCert{i}", expiryDate);
                collection.Add(cert);
            }

            return collection;
        }

        private static X509Certificate2 CreateSelfSignedCertificate(string subjectName, DateTime notAfter)
        {
            var distinguishedName = new X500DistinguishedName($"CN={subjectName}");

            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var request = new CertificateRequest(distinguishedName, rsa,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

            var certificate = request.CreateSelfSigned(DateTime.UtcNow.AddDays(-1), notAfter);
            return certificate;
        }
    }
}
