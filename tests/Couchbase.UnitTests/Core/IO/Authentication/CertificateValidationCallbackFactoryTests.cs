using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
#if NET7_0_OR_GREATER
using Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;
#endif

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication;

/// <summary>
/// Covers how <see cref="CertificateValidationCallbackFactory"/> composes the callback, which is the only
/// place the user callback, the per-protocol name mismatch flags and the trust anchor source are chosen.
/// </summary>
public class CertificateValidationCallbackFactoryTests
{
    [Fact]
    public void CreateForKv_ReturnsUserCallback_WhenConfigured()
    {
        RemoteCertificateValidationCallback userCallback = (_, _, _, _) => true;
        var factory = CreateFactory(new TlsSettings { KvCertificateValidationCallback = userCallback });

        Assert.Same(userCallback, factory.CreateForKv());
    }

    [Fact]
    public void CreateForHttp_ReturnsUserCallback_WhenConfigured()
    {
        RemoteCertificateValidationCallback userCallback = (_, _, _, _) => true;
        var factory = CreateFactory(new TlsSettings { HttpCertificateValidationCallback = userCallback });

        Assert.Same(userCallback, factory.CreateForHttp());
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void CreateForKv_UsesKvNameMismatchFlag_NotHttp()
    {
        // The two protocols carry their own flag and must not read each other's.
        var factory = CreateFactory(new TlsSettings
        {
            KvIgnoreRemoteCertificateNameMismatch = true,
            HttpIgnoreRemoteCertificateNameMismatch = false,
        });

        using var leaf = TlsTestPki.CreateServerLeaf("Flag Test Leaf", "localhost");
        using var kvChain = new X509Chain();
        using var httpChain = new X509Chain();

        Assert.True(
            factory.CreateForKv()(new object(), leaf, kvChain, SslPolicyErrors.RemoteCertificateNameMismatch),
            "The KV callback must honour KvIgnoreRemoteCertificateNameMismatch.");
        Assert.False(
            factory.CreateForHttp()(new object(), leaf, httpChain, SslPolicyErrors.RemoteCertificateNameMismatch),
            "The HTTP callback must keep rejecting a name mismatch.");
    }
#endif

    [Fact]
    public void Create_QueriesTrustedCertificateFactory_OnEveryCall()
    {
        // Server trust can rotate, so every new connection has to ask the factory again.
        var certificateFactory = new Mock<ICertificateFactory>();
        certificateFactory.Setup(f => f.GetCertificates()).Returns(new X509Certificate2Collection());

        var factory = CreateFactory(new TlsSettings
        {
            TrustedServerCertificateFactory = certificateFactory.Object,
        });

        factory.CreateForKv();
        factory.CreateForKv();
        factory.CreateForHttp();

        certificateFactory.Verify(f => f.GetCertificates(), Times.Exactly(3));
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void Create_WithoutTrustedFactory_FallsBackToDefaults()
    {
        // With nothing configured the anchors are the bundled Capella CA, which must not accept a private
        // certificate.
        var factory = CreateFactory(new TlsSettings());

        using var privateLeaf = TlsTestPki.CreateServerLeaf("Private Leaf", "localhost");
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        var accepted = factory.CreateForKv()(
            new object(), privateLeaf, chain, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.False(accepted, "A private certificate must not validate against the bundled Capella defaults.");
    }
#endif

    private static CertificateValidationCallbackFactory CreateFactory(TlsSettings tlsSettings)
    {
        var clusterOptions = new ClusterOptions { TlsSettings = tlsSettings };

        return new CertificateValidationCallbackFactory(
            clusterOptions,
            NullLogger<CertificateValidationCallbackFactory>.Instance,
            new Mock<IRedactor>().Object);
    }
}
