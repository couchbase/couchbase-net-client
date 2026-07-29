#if NET6_0_OR_GREATER

using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509;

/// <summary>
/// Exercises <see cref="CertificateFactory.GetValidatorWithPredefinedCertificates"/> against a real
/// PKI hierarchy (root -> intermediate -> leaf) over a loopback TLS handshake.
///
/// The point of these tests is to drive the SDK's *actual* validation callback the same way
/// <c>SslStream</c> drives it in production: SslStream receives the server-presented (wire) chain,
/// pre-populates the <see cref="X509Chain"/> handed to the callback, and the SDK decides accept/reject.
/// We deliberately do NOT supply a custom callback that builds its own chain, so the connection result
/// IS the SDK's verdict.
///
/// A rejected handshake is not on its own evidence that the validator rejected. The harness records
/// whether the validator ran, what it returned, and whether it threw, so a negative test cannot pass
/// because of a throw, a listener failure or a server-side error.
/// </summary>
public sealed class CertificateFactoryWireIntermediateTests : IDisposable
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);

    private readonly ITestOutputHelper _output;
    private readonly X509Certificate2 _root;
    private readonly X509Certificate2 _intermediate;
    private readonly X509Certificate2 _leaf;            // CA-issued, has private key, used to serve TLS
    private readonly X509Certificate2 _unrelatedRoot;
    private readonly X509Certificate2 _selfSignedLeaf;  // self-signed server cert (its own anchor)

    private const string LeafDnsName = "localhost";

    public CertificateFactoryWireIntermediateTests(ITestOutputHelper output)
    {
        _output = output;

        _root = CreateCa("Test Wire Root CA", issuer: null);
        _intermediate = CreateCa("Test Wire Intermediate CA", issuer: _root);
        _leaf = CreateLeaf("Test Wire Leaf", LeafDnsName, issuer: _intermediate);
        _unrelatedRoot = CreateCa("Unrelated Root CA", issuer: null);
        _selfSignedLeaf = CreateSelfSignedLeaf("Self Signed Node", LeafDnsName);
    }

    [Fact]
    public async Task RootOnly_InTrustBundle_Accepts_BecauseWireIntermediateIsHonoured()
    {
        // Only the root is trusted. The leaf can only validate if the SDK honours
        // the intermediate the server presented on the wire. This is the behaviour the fix adds;
        // without it, the retry rebuilt the chain from the root-only bundle and produced a
        // PartialChain, rejecting a perfectly valid leaf.
        var result = await RunHandshake(_leaf, wireExtras: new[] { _intermediate }, CopyOf(_root));
        AssertAccepted(result,
            "Validator should accept a leaf signed by a wire-presented intermediate when the root is trusted.");
    }

    [Fact]
    public async Task IntermediateOnly_InTrustBundle_Rejects()
    {
        // Only the intermediate is in the trust bundle (no root). Chain validation should fail.
        // .NET's X509ChainTrustMode.CustomRootTrust only honours self-signed certificates placed
        // in CustomTrustStore as trust anchors.
        // A non-self-signed intermediate is not treated as an anchor, and its own issuer (the root) is not trusted, so the chain cannot terminate
        // at a trusted root. This proves the validator is not silently accepting anything.
        var result = await RunHandshake(_leaf, wireExtras: new[] { _intermediate }, CopyOf(_intermediate));
        AssertRejectedByValidator(result,
            "Validator must reject when only the (non-anchor) intermediate is trusted and the root is absent.");
    }

    [Fact]
    public async Task UnrelatedRoot_InTrustBundle_Rejects_TrustAnchorsAreEnforced()
    {
        // An unrelated root is trusted. The real chain cannot terminate at it, so it is rejected.
        var result = await RunHandshake(_leaf, wireExtras: new[] { _intermediate }, CopyOf(_unrelatedRoot));
        AssertRejectedByValidator(result,
            "Validator must reject when the trust bundle contains only an unrelated root.");
    }

    [Fact]
    public async Task SelfSignedLeaf_InTrustBundle_Accepts_ExistingUseCaseStillWorks()
    {
        // The node serves a self-signed cert and the user pins that exact cert via WithTrustedServerCertificates.
        // The self-signed cert is its own trust anchor, so CustomRootTrust accepts it
        var result = await RunHandshake(_selfSignedLeaf, wireExtras: Array.Empty<X509Certificate2>(), CopyOf(_selfSignedLeaf));
        AssertAccepted(result,
            "Validator should accept when the presented self-signed leaf is itself in the trust bundle.");
    }

    [Fact]
    public async Task DifferentRealCa_NotInChain_Rejects()
    {
        // A real CA that did not sign the leaf. Proves that merely having "a" CA in
        // the bundle should indeed fail
        using var otherRoot = CreateCa("Other Real Root CA", issuer: null);
        var result = await RunHandshake(_leaf, wireExtras: new[] { _intermediate }, CopyOf(otherRoot));
        AssertRejectedByValidator(result,
            "A real-but-unrelated CA in the trust bundle must not validate the leaf's chain.");
    }

    [Fact]
    public async Task CaIssuedLeafPinnedAlone_Rejects_DocumentsCustomRootTrustLimitation()
    {
        // Observation (not a regression): pinning ONLY a CA-issued (non-self-signed) leaf does not work,
        // because CustomRootTrust will not treat the non-self-signed leaf as an anchor and its issuer
        // chain is untrusted. Distinct from SelfSignedLeaf_InTrustBundle_Accepts, where the leaf IS a
        // self-signed anchor. Captured so the difference is intentional and visible.
        var result = await RunHandshake(_leaf, wireExtras: new[] { _intermediate }, CopyOf(_leaf));
        AssertRejectedByValidator(result,
            "Pinning only a CA-issued (non-self-signed) leaf is not honoured by CustomRootTrust.");
    }

    /// <summary>
    /// The handshake verdict plus what the SDK validator actually did, so a test can distinguish a
    /// deliberate rejection from a throw or from a failure that never reached the validator.
    /// </summary>
    private sealed record HandshakeResult(
        bool Accepted,
        bool ValidatorInvoked,
        bool? ValidatorVerdict,
        Exception? ValidatorException);

    private void AssertAccepted(HandshakeResult result, string because)
    {
        AssertValidatorRanCleanly(result, because);
        Assert.True(result.ValidatorVerdict, because);
        Assert.True(result.Accepted, because);
    }

    private void AssertRejectedByValidator(HandshakeResult result, string because)
    {
        AssertValidatorRanCleanly(result, because);
        Assert.False(result.ValidatorVerdict, because);
        Assert.False(result.Accepted, because);
    }

    private void AssertValidatorRanCleanly(HandshakeResult result, string because)
    {
        if (!result.ValidatorInvoked)
        {
            Assert.Fail($"{because}{Environment.NewLine}" +
                        "The validator was never invoked, so the handshake result says nothing about it.");
        }

        if (result.ValidatorException is not null)
        {
            Assert.Fail($"{because}{Environment.NewLine}" +
                        $"The validator threw instead of returning a verdict: {result.ValidatorException}");
        }
    }

    /// <summary>
    /// Stand up a loopback TLS server that presents <paramref name="serverLeaf"/> with
    /// <paramref name="wireExtras"/> as the wire chain, then connect a client whose validation callback
    /// is the SDK's actual validator configured with <paramref name="trustBundle"/>.
    /// </summary>
    private async Task<HandshakeResult> RunHandshake(
        X509Certificate2 serverLeaf, X509Certificate2[] wireExtras, params X509Certificate2[] trustBundle)
    {
        var bundle = new X509Certificate2Collection(trustBundle);

        var wireCollection = new X509Certificate2Collection();
        foreach (var extra in wireExtras)
        {
            wireCollection.Add(CopyOf(extra));
        }

        // Server presents serverLeaf + wireExtras on the wire (the root, if any, is intentionally not sent).
        var serverContext = SslStreamCertificateContext.Create(serverLeaf, wireCollection, offline: true);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var cts = new CancellationTokenSource(HandshakeTimeout);

        Exception? serverException = null;
        var serverTask = Task.Run(async () =>
        {
            try
            {
                using var conn = await listener.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);
                using var sslServer = new SslStream(conn.GetStream(), leaveInnerStreamOpen: false);
                await sslServer.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificateContext = serverContext,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificateRequired = false,
                }, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The client rejecting the cert tears down the handshake, so this is expected for
                // negative cases. Kept rather than swallowed, and re-surfaced below only when the
                // client failed for a reason other than a clean rejection.
                serverException = ex;
            }
        }, CancellationToken.None);

        var innerValidator = CertificateFactory.GetValidatorWithPredefinedCertificates(bundle, logger: null, redactor: null);

        var invoked = false;
        bool? verdict = null;
        Exception? validatorException = null;

        bool RecordingValidator(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
        {
            invoked = true;
            try
            {
                var accepted = innerValidator(sender, certificate, chain, errors);
                verdict = accepted;
                return accepted;
            }
            catch (Exception ex)
            {
                validatorException = ex;
                throw;
            }
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token).ConfigureAwait(false);
            using var sslClient = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, RecordingValidator);

            var accepted = false;
            try
            {
                // targetHost == leaf SAN, so there is no name mismatch.
                // chain validation is the only thing under test
                await sslClient.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions { TargetHost = LeafDnsName }, cts.Token).ConfigureAwait(false);
                accepted = true;
            }
            catch (AuthenticationException ex)
            {
                _output.WriteLine($"Handshake rejected: {ex.Message}");
            }

            if (!invoked)
            {
                // Nothing was validated, so the failure is environmental. Surface the server-side cause.
                throw new InvalidOperationException(
                    "TLS handshake failed before the SDK validator was invoked.", serverException);
            }

            return new HandshakeResult(accepted, invoked, verdict, validatorException);
        }
        finally
        {
            listener.Stop();
            try
            {
                await serverTask.ConfigureAwait(false);
            }
            catch
            {
                // Already captured in serverException.
            }

            foreach (var cert in wireCollection)
            {
                cert.Dispose();
            }

            foreach (var cert in bundle)
            {
                cert.Dispose();
            }
        }
    }

    private static X509Certificate2 CreateCa(string commonName, X509Certificate2? issuer)
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);

        if (issuer is null)
        {
            // self-signed root, keep the private key so it can sign children
            return request.CreateSelfSigned(notBefore, DateTimeOffset.UtcNow.AddYears(10));
        }

        // Keep child validity strictly inside the issuer's window (X509 disallows a child notAfter
        // later than its issuer's, and the per-call UtcNow drift would otherwise trip that by ~1s).
        var notAfter = new DateTimeOffset(issuer.NotAfter).AddDays(-1);
        var signed = request.Create(issuer, notBefore, notAfter, NewSerial());
        return signed.CopyWithPrivateKey(rsa);
    }

    private static X509Certificate2 CreateLeaf(string commonName, string dnsName, X509Certificate2 issuer)
    {
        var rsa = RSA.Create(2048);
        var request = BuildServerRequest(commonName, dnsName, rsa);
        var signed = request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2), NewSerial());
        return signed.CopyWithPrivateKey(rsa);
    }

    private static X509Certificate2 CreateSelfSignedLeaf(string commonName, string dnsName)
    {
        var rsa = RSA.Create(2048);
        var request = BuildServerRequest(commonName, dnsName, rsa);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
    }

    private static CertificateRequest BuildServerRequest(string commonName, string dnsName, RSA rsa)
    {
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") /* serverAuth */ }, critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        request.CertificateExtensions.Add(san.Build());

        return request;
    }

    private static byte[] NewSerial() => Guid.NewGuid().ToByteArray();

    /// <summary>
    /// Independent copy of a certificate (non-obsolete copy constructor). Used for trust-bundle / wire
    /// entries so the shared per-test certificate fields are not disposed when SslStream tears down the
    /// chain it built.
    /// </summary>
    private static X509Certificate2 CopyOf(X509Certificate2 cert) => new X509Certificate2(cert);

    public void Dispose()
    {
        _root.Dispose();
        _intermediate.Dispose();
        _leaf.Dispose();
        _unrelatedRoot.Dispose();
        _selfSignedLeaf.Dispose();
    }
}

#endif
