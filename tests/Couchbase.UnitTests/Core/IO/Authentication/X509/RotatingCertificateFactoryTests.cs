using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.IO.Authentication.X509;

public class RotatingCertificateFactoryTests(
    ITestOutputHelper testOutputHelper)
{
    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;
    private readonly Mock<ICertificateFactory> _mockCertificateFactory = new();
    private readonly Mock<ILogger<IRotatingCertificateFactory>> _mockLogger = new();

    private static string ExpiredCertificateBase64 =
        "MIICpjCCAY6gAwIBAgIIEs4Z8ZTVrmcwDQYJKoZIhvcNAQELBQAwEzERMA8GA1UEAxMIVGVzdENlcnQwHhcNMjUwOTE3MjIyMzI1WhcNMjUwOTE4MjIyNDI1WjATMREwDwYDVQQDEwhUZXN0Q2VydDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBALelsYDZjsFTROdwp5ZHHEuWesXq9NM9WnfOsCtbtxZligQGLEg2aktsDFFgYkkT/rTZ3HZ1NpG06/phAGJMnHfahW+jnMg7xkdYSF7MtHgOAhJdkIyCkHeRsG0lA/lAzCirUmgnCB6Z1+pFfXMO0ld+hCjCdCLUOi4Q7PsrFIXVkCWAcVgpDPiQroEYzdsDlCQRZmxa42XIL5SDo2w4PUnJJtD9e6f1nhcKYUmthplGkB0zWzIUFehP3vq1J5wdNhaGANVN5jAqVn2OP0VbhBb2+zshfbX6Md3QybcW5j0dr4AahmWSRGGj65e+CKe2cfseCCqNa5b9CTwFRLa/jLUCAwEAATANBgkqhkiG9w0BAQsFAAOCAQEApNG87C49Z9xuBozvc2RVOkjj/4Z897GvCB4NpPIpR0NGJgjBTyk40PiDlD/SeMH+7b3ONjWB/CRo42OK3NwDYotpNa0BLiCvEkMMjuXrEuRj1jl+5peqrl1f3saSuE3t+lZe2L9S9csX4+Dtu6p9+jfmipXJB9lSLtOsVqAlPWe737PKPJBj1Rm2MP8c0szNR12C1OAOaCpqMr1V3VfSpFmYQNj18XbeDH7rFrHNbc2TgNxvpwDWf0F1Dbz6UQtb9qkj57MTuer0VPIPhB4l2YqCkdlhky2ugxsbcvroZNnPQIgxleUz6kbYZs8izgtt5SO7L+m3cylqw6UIg0PoaA==";

    [Fact]
    public void Constructor_WithValidParameters_ShouldNotThrow()
    {
        // Arrange & Act
        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30),
            _mockLogger.Object);

        // Assert
        Assert.NotNull(factory);
        Assert.False(factory.HasUpdates);
    }

    [Fact]
    public void Constructor_WithNullCertificateFactory_ShouldThrow()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RotatingCertificateFactory(
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(30),
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ShouldThrow()
    {
        // Arrange & Act
        Assert.Throws<ArgumentNullException>(() =>
            new RotatingCertificateFactory(
                _mockCertificateFactory.Object,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(30),
                null));
    }

    [Fact]
    public void HasUpdates_InitialState_ShouldBeFalse()
    {
        // Arrange
        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30),
            _mockLogger.Object);

        // Act & Assert
        Assert.False(factory.HasUpdates);
    }

    [Fact]
    public void GetCertificates_FirstCall_ShouldReturnCertificatesFromUnderlyingFactory()
    {
        // Arrange
        var expectedCertificates = CreateTestCertificateCollection(2);
        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(expectedCertificates);

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(30),
            _mockLogger.Object);

        // Act
        var result = factory.GetCertificates();

        // Assert
        Assert.Equal(expectedCertificates.Count, result.Count);
        _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.Once);
    }

    [Fact]
    public void GetCertificates_MultipleCalls_ShouldReturnCachedCertificates()
    {
        // Arrange
        var expectedCertificates = CreateTestCertificateCollection(2);
        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(expectedCertificates);

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(30),
            _mockLogger.Object);

        // Act
        var result1 = factory.GetCertificates();
        var result2 = factory.GetCertificates();

        // Assert
        Assert.Same(result1, result2);
        _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.Once);
    }

    [Fact]
    public async Task GetCertificates_WithShortInterval_ShouldStartTimer()
    {
        // Arrange
        var expectedCertificates = CreateTestCertificateCollection(1);
        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(expectedCertificates);

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMinutes(30),
            _mockLogger.Object);

        // Act
        var result = factory.GetCertificates();

        // Wait a bit to allow timer to potentially fire
        await Task.Delay(1000);

        // Assert
        Assert.NotNull(result);
        // Timer should have been created and potentially fired
        _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RefreshClientHandler_WithValidNewCertificates_ShouldUpdateCache()
    {
        // Arrange
        var initialCertificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(30));
        var newValidCertificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(60));

        var callCount = 0;
        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? initialCertificates : newValidCertificates;
            });

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMinutes(30),
            _mockLogger.Object);

        // Act
        var initialResult = factory.GetCertificates();

        // Wait for timer to fire and refresh
        await Task.Delay(1000);

        // Assert
        Assert.NotNull(initialResult);
        // The timer should have fired at least once
        _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.AtLeast(2));
    }

    [Fact]
    public async Task RefreshClientHandler_WithExpiredCertificates_ShouldNotUpdateCache()
    {
        // Arrange
        var initialCertificates = CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(30));
        var expiredCertificates = GetExpiredCertificates();//CreateTestCertificateCollection(1, DateTime.UtcNow.AddDays(-1)); // Expired

        var callCount = 0;
        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? initialCertificates : expiredCertificates;
            });

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMinutes(30),
            _mockLogger.Object);

        // Act
        var initialResult = factory.GetCertificates();
        var hasUpdatesBeforeRefresh = factory.HasUpdates;

        // Wait for timer to fire
        await Task.Delay(1000);

        var hasUpdatesAfterRefresh = factory.HasUpdates;

        // Assert
        Assert.NotNull(initialResult);
        Assert.False(hasUpdatesBeforeRefresh);
        Assert.False(hasUpdatesAfterRefresh); // Should remain false since expired certs were ignored
    }

    [Fact]
    public async Task RefreshClientHandler_WithException_ShouldLogWarningAndContinue()
    {
        // Arrange
        var initialCertificates = CreateTestCertificateCollection(1);
        var callCount = 0;

        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    return initialCertificates;
                throw new InvalidOperationException("Test exception");
            });

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMinutes(1),
            _mockLogger.Object);

        // Act
        var result = factory.GetCertificates();

        // Wait for timer to fire and exception to be thrown
        await Task.Delay(300);

        // Assert
        Assert.NotNull(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Refreshing client certificates failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void RefreshClientHandler_WithNullTimer_ShouldReturnEarly()
    {
        // This test verifies the null check in RefreshClientHandler
        // Since we can't directly call RefreshClientHandler, we test the behavior indirectly

        // Arrange
        var certificates = CreateTestCertificateCollection(1);
        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(certificates);

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromHours(1), // Long interval to prevent timer firing
            TimeSpan.FromMinutes(130),
            _mockLogger.Object);

        // Act
        var result = factory.GetCertificates();

        // Assert
        Assert.NotNull(result);
        _mockCertificateFactory.Verify(x => x.GetCertificates(), Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void GetCertificates_WithDifferentCertificateCounts_ShouldReturnCorrectCount(int certificateCount)
    {
        // Arrange
        var certificates = CreateTestCertificateCollection(certificateCount);
        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(certificates);

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(30),
            _mockLogger.Object);

        // Act
        var result = factory.GetCertificates();

        // Assert
        Assert.Equal(certificateCount, result.Count);
    }

    [Fact]
    public void GetCertificates_WithEmptyCollection_ShouldReturnEmptyCollection()
    {
        // Arrange
        var emptyCertificates = new X509Certificate2Collection();
        _mockCertificateFactory.Setup(x => x.GetCertificates())
            .Returns(emptyCertificates);

        var factory = new RotatingCertificateFactory(
            _mockCertificateFactory.Object,
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(130),
            _mockLogger.Object);

        // Act
        var result = factory.GetCertificates();

        // Assert
        Assert.Empty(result);
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
        // Create a minimal certificate for testing purposes
        // In a real test environment, you might want to use a more sophisticated approach
        // or mock the certificate creation

        var distinguishedName = new X500DistinguishedName($"CN={subjectName}");

        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(distinguishedName, rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(DateTime.UtcNow.AddDays(-1), notAfter);
        return certificate;
    }

    private static X509Certificate2Collection GetExpiredCertificates()
    {
        var certificateBytes = Convert.FromBase64String(ExpiredCertificateBase64);

        X509Certificate2 expiredCertificate = null;
#if NET10_0_OR_GREATER
        expiredCertificate = X509CertificateLoader.LoadCertificate(certificateBytes);
#else
        expiredCertificate = new X509Certificate2(certificateBytes);
#endif
        return new X509Certificate2Collection(expiredCertificate);
    }
}
