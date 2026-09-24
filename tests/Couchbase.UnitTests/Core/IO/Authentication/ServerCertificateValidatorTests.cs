#if NET7_0_OR_GREATER

using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.Logging;
using Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication;

/// <summary>
/// Exercises <see cref="ServerCertificateValidator"/> against real PKI hierarchies, mostly over a loopback
/// TLS handshake so SslStream builds the chain the way production does.
/// </summary>
/// <remarks>
/// Covers the "honour wire intermediates" fix (NCBC-4216), where a leaf that depends on a server-presented
/// intermediate must validate when only the root is in the trust bundle. Also pins the ownership invariant
/// from NCBC-4120, that a trust bundle survives being validated against.
/// </remarks>
public sealed class ServerCertificateValidatorTests : IDisposable
{
    private const string LeafDnsName = "localhost";
    private const string WrongHostName = "wrong.example.com";

    private readonly ITestOutputHelper _output;
    private readonly X509Certificate2 _root;
    private readonly X509Certificate2 _intermediate;
    private readonly X509Certificate2 _leaf;            // CA-issued, has private key, used to serve TLS
    private readonly X509Certificate2 _unrelatedRoot;
    private readonly X509Certificate2 _selfSignedLeaf;  // self-signed server cert (its own anchor)

    public ServerCertificateValidatorTests(ITestOutputHelper output)
    {
        _output = output;

        _root = TlsTestPki.CreateCa("Test Wire Root CA");
        _intermediate = TlsTestPki.CreateCa("Test Wire Intermediate CA", issuer: _root);
        _leaf = TlsTestPki.CreateServerLeaf("Test Wire Leaf", LeafDnsName, issuer: _intermediate);
        _unrelatedRoot = TlsTestPki.CreateCa("Unrelated Root CA");
        _selfSignedLeaf = TlsTestPki.CreateServerLeaf("Self Signed Node", LeafDnsName);
    }

    [Fact]
    public async Task RootOnly_InTrustBundle_Accepts_BecauseWireIntermediateIsHonoured()
    {
        // Only the root is trusted. The leaf can only validate if the validator honours the intermediate
        // the server presented on the wire. Without that the chain is a PartialChain and a perfectly valid
        // leaf is rejected.
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.Accepted(result,
            "Validator should accept a leaf signed by a wire-presented intermediate when the root is trusted.");
    }

    [Fact]
    public async Task SubordinateCaWithSameSubjectAsRoot_RootOnly_InTrustBundle_Accepts()
    {
        // The NCBC-4275 shape. A Capella disaster recovery control plane issues node certificates from a
        // subordinate CA that carries the same subject name as the root but a different key. The issuer
        // has to be matched by key identifier, not by name, and the subordinate only exists on the wire.
        using var subordinate = TlsTestPki.CreateCa("Test Wire Root CA", issuer: _root);
        Assert.Equal(_root.SubjectName.RawData, subordinate.SubjectName.RawData);
        using var drLeaf = TlsTestPki.CreateServerLeaf("DR Node Leaf", LeafDnsName, issuer: subordinate);
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(drLeaf, new[] { subordinate }, bundle);

        HandshakeAssert.Accepted(result,
            "A leaf issued by a subordinate CA that shares the root's subject name should validate against the root.");
    }

    [Fact]
    public async Task TwoLevelIntermediateChain_RootOnly_InTrustBundle_Accepts()
    {
        // root -> ica1 -> ica2 -> leaf, with both intermediates presented on the wire. Proves the fix is
        // not limited to a single intermediate.
        using var ica1 = TlsTestPki.CreateCa("Depth Test ICA 1", issuer: _root);
        using var ica2 = TlsTestPki.CreateCa("Depth Test ICA 2", issuer: ica1);
        using var deepLeaf = TlsTestPki.CreateServerLeaf("Depth Test Leaf", LeafDnsName, issuer: ica2);
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(deepLeaf, new[] { ica2, ica1 }, bundle);

        HandshakeAssert.Accepted(result, "A two-level intermediate chain should validate against the root alone.");
    }

    [Fact]
    public async Task MultiEntryTrustBundle_Accepts()
    {
        // The bundle holds the real anchor plus the intermediate. The extra non-anchor entry must not stop
        // the chain validating. The root cannot be put on the wire, see WireCertificatesNeverBecomeTrustAnchors.
        using var bundle = new TrustBundle(_root, _intermediate);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.Accepted(result,
            "A bundle holding the real anchor alongside a non-anchor should validate.");
    }

    [Fact]
    public async Task RepeatedHandshakes_ReuseOneTrustBundle_AndLeaveItUsable()
    {
        // The NCBC-4120 shape. Certificates placed in a chain store used to be the caller's own instances,
        // which .NET could dispose once the callback returned, breaking every later handshake on the same
        // bundle. Three handshakes on one bundle, then the bundle must still be readable.
        using var bundle = new TrustBundle(_root);
        var expectedThumbprint = bundle[0].Thumbprint;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var result = await Handshake(_leaf, new[] { _intermediate }, bundle);
            HandshakeAssert.Accepted(result, $"Handshake {attempt} of 3 on a reused trust bundle should be accepted.");
        }

        Assert.Equal(expectedThumbprint, bundle[0].Thumbprint);
        Assert.NotEmpty(bundle[0].RawData);
    }

    [Fact]
    public async Task IntermediateOnly_InTrustBundle_Rejects()
    {
        // Only the intermediate is in the trust bundle (no root). CustomRootTrust honours only self-signed
        // certificates in CustomTrustStore as anchors, so a non-self-signed intermediate is not an anchor
        // and its own issuer is untrusted. Proves the validator is not accepting anything.
        using var bundle = new TrustBundle(_intermediate);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "Validator must reject when only the non-anchor intermediate is trusted and the root is absent.");
    }

    [Fact]
    public async Task UnrelatedRoot_InTrustBundle_Rejects()
    {
        using var bundle = new TrustBundle(_unrelatedRoot);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "Validator must reject when the trust bundle contains only an unrelated root.");
    }

    [Fact]
    public async Task DifferentRealCa_NotInChain_Rejects()
    {
        // A real CA that did not sign the leaf. Merely having "a" CA in the bundle must not be enough.
        using var otherRoot = TlsTestPki.CreateCa("Other Real Root CA");
        using var bundle = new TrustBundle(otherRoot);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "A real but unrelated CA in the trust bundle must not validate the leaf's chain.");
    }

    [Fact]
    public async Task SelfSignedLeaf_InTrustBundle_Accepts()
    {
        // The node serves a self-signed certificate and the user pins that exact certificate. It is its own
        // trust anchor, so CustomRootTrust accepts it.
        using var bundle = new TrustBundle(_selfSignedLeaf);

        var result = await Handshake(_selfSignedLeaf, Array.Empty<X509Certificate2>(), bundle);

        HandshakeAssert.Accepted(result,
            "Validator should accept when the presented self-signed leaf is itself in the trust bundle.");
    }

    [Fact]
    public async Task CaIssuedLeafPinnedAlone_Rejects_DocumentsCustomRootTrustLimitation()
    {
        // Observation, not a regression. Pinning only a CA-issued (non-self-signed) leaf does not work,
        // because CustomRootTrust will not treat it as an anchor and its issuer chain is untrusted. Distinct
        // from SelfSignedLeaf_InTrustBundle_Accepts, where the leaf IS a self-signed anchor. Captured so the
        // difference stays visible rather than being rediscovered.
        using var bundle = new TrustBundle(_leaf);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "Pinning only a CA-issued (non-self-signed) leaf is not honoured by CustomRootTrust.");
    }

    [Fact]
    public async Task ExpiredLeaf_Rejects()
    {
        using var expiredLeaf = TlsTestPki.CreateServerLeaf(
            "Expired Leaf", LeafDnsName, issuer: _intermediate,
            notBefore: DateTimeOffset.UtcNow.AddDays(-30),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(expiredLeaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result, "An expired leaf must be rejected even when its root is trusted.");
    }

    [Fact]
    public async Task ExpiredIntermediate_Rejects()
    {
        // The leaf cannot outlive its issuer, so it expires with it. An expired CA in the path must be
        // rejected rather than skipped.
        using var expiredIntermediate = TlsTestPki.CreateCa(
            "Expired Intermediate CA", issuer: _root,
            notBefore: DateTimeOffset.UtcNow.AddDays(-30),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        using var leafUnderExpiredCa = TlsTestPki.CreateServerLeaf(
            "Leaf Under Expired CA", LeafDnsName, issuer: expiredIntermediate,
            notBefore: DateTimeOffset.UtcNow.AddDays(-30));
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(leafUnderExpiredCa, new[] { expiredIntermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result, "An expired intermediate must be rejected.");
    }

    [Fact]
    public async Task PathLengthConstraintViolation_Rejects()
    {
        // A root that permits zero intermediate CAs below it, then a chain that inserts one.
        using var constrainedRoot = TlsTestPki.CreateCa("PathLen Zero Root CA", pathLengthConstraint: 0);
        using var forbiddenIntermediate = TlsTestPki.CreateCa("PathLen Violating ICA", issuer: constrainedRoot);
        using var leafBehindIt = TlsTestPki.CreateServerLeaf("PathLen Leaf", LeafDnsName, issuer: forbiddenIntermediate);
        using var bundle = new TrustBundle(constrainedRoot);

        var result = await Handshake(leafBehindIt, new[] { forbiddenIntermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "A chain that violates the root's pathLenConstraint must be rejected.");
    }

    [Fact]
    public async Task ClientAuthOnlyLeaf_Rejects()
    {
        using var clientAuthLeaf = TlsTestPki.CreateServerLeaf(
            "Client Auth Leaf", LeafDnsName, issuer: _intermediate,
            ekus: new OidCollection { new Oid(TlsTestPki.ClientAuthOid) });
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(clientAuthLeaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result, "A leaf limited to clientAuth must not serve TLS.");
    }

    [Fact]
    public async Task ClientAuthOnlyIntermediate_Rejects()
    {
        using var clientAuthIntermediate = TlsTestPki.CreateCa(
            "Client Auth Intermediate CA", issuer: _root,
            ekus: new OidCollection { new Oid(TlsTestPki.ClientAuthOid) });
        using var leafUnderIt = TlsTestPki.CreateServerLeaf(
            "Leaf Under Client Auth CA", LeafDnsName, issuer: clientAuthIntermediate);
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(leafUnderIt, new[] { clientAuthIntermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "An intermediate limited to clientAuth must not issue a TLS server certificate.");
    }

    [Fact]
    public void WireCertificatesNeverBecomeTrustAnchors()
    {
        // The whole rogue chain, its self-signed root included, is placed where SslStream puts the
        // server-presented certificates. If anything in the ExtraStore were promoted into the trust store
        // this would be accepted. Driven directly rather than over a handshake, because
        // SslStreamCertificateContext drops self-signed certificates from the chain it serves, so a rogue
        // root can never reach the validator that way.
        using var rogueRoot = TlsTestPki.CreateCa("Rogue Root CA");
        using var rogueIntermediate = TlsTestPki.CreateCa("Rogue Intermediate CA", issuer: rogueRoot);
        using var rogueLeaf = TlsTestPki.CreateServerLeaf("Rogue Leaf", LeafDnsName, issuer: rogueIntermediate);
        using var bundle = new TrustBundle(_root);

        var validator = CreateValidator(bundle.Certificates);

        var accepted = DirectChain.Validate(validator.Validate, rogueLeaf, new[] { rogueIntermediate, rogueRoot });

        Assert.False(accepted,
            "A server-presented root must not become a trust anchor for the chain that presented it.");
    }

    [Fact]
    public void WireCertificates_AreNotTrustedWhenNoAnchorsAreConfigured()
    {
        // The strongest form of the same property. With an empty trust bundle there is nothing to chain to,
        // so a fully server-supplied chain has only itself to offer.
        using var rogueRoot = TlsTestPki.CreateCa("Anchorless Rogue Root CA");
        using var rogueIntermediate = TlsTestPki.CreateCa("Anchorless Rogue Intermediate CA", issuer: rogueRoot);
        using var rogueLeaf = TlsTestPki.CreateServerLeaf("Anchorless Rogue Leaf", LeafDnsName, issuer: rogueIntermediate);

        var validator = CreateValidator(new X509Certificate2Collection());

        var accepted = DirectChain.Validate(validator.Validate, rogueLeaf, new[] { rogueIntermediate, rogueRoot });

        Assert.False(accepted, "With no configured trust anchors nothing the server sends may be trusted.");
    }

    [Fact]
    public async Task ValidChain_MatchingHostName_Accepts()
    {
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle, targetHost: LeafDnsName);

        HandshakeAssert.Accepted(result, "A valid chain served under its own SAN should be accepted.");
    }

    [Fact]
    public async Task HostNameMismatch_NotIgnored_Rejects()
    {
        // The chain itself is fine, so only the hostname gate can reject here.
        using var bundle = new TrustBundle(_root);
        var logger = new RecordingLogger(debugEnabled: false);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle, targetHost: WrongHostName, logger: logger);

        HandshakeAssert.RejectedByValidator(result,
            "A certificate that does not match the requested host must be rejected by default.");
        Assert.Contains(logger.Messages, m => m.Contains(WrongHostName) && m.Contains(_leaf.Subject));
    }

    [Fact]
    public async Task ChainErrorOnly_WrongHost_Rejects()
    {
        // Windows reports only the chain error when the host is wrong and the CA is not in the OS store.
        using var bundle = new TrustBundle(_root);
        var validator = CreateValidator(bundle.Certificates);

        var result = await TlsLoopback.RunAsync(_leaf, new[] { _intermediate },
            (sender, certificate, chain, errors) =>
                validator.Validate(sender, certificate, chain, errors & ~SslPolicyErrors.RemoteCertificateNameMismatch),
            WrongHostName, _output);

        HandshakeAssert.RejectedByValidator(result,
            "A wrong host must be rejected even when the platform reports only a chain error.");
    }

    [Fact]
    public async Task WildcardLeaf_MatchingHostName_Accepts()
    {
        // Capella issues wildcard node certificates, so the name check has to honour them.
        using var wildcardLeaf = TlsTestPki.CreateServerLeaf("Wildcard Leaf", "*.example.com", issuer: _intermediate);
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(wildcardLeaf, new[] { _intermediate }, bundle, targetHost: "node1.example.com");

        HandshakeAssert.Accepted(result, "A wildcard certificate should be accepted for a host in its domain.");
    }

    [Fact]
    public async Task WildcardLeaf_HostNameOneLabelDeeper_Rejects()
    {
        // A wildcard covers a single label, so it stops at the first dot.
        using var wildcardLeaf = TlsTestPki.CreateServerLeaf("Wildcard Leaf", "*.example.com", issuer: _intermediate);
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(wildcardLeaf, new[] { _intermediate }, bundle, targetHost: "node1.dc1.example.com");

        HandshakeAssert.RejectedByValidator(result, "A wildcard must match a single label, not a sub domain.");
    }

    [Fact]
    public async Task HostNameMismatch_Ignored_Accepts()
    {
        // The ignore flag opts out of the hostname check only. The chain is still validated.
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle,
            ignoreNameMismatch: true, targetHost: WrongHostName);

        HandshakeAssert.Accepted(result,
            "With the name mismatch ignored, a valid chain should be accepted despite the wrong host.");
    }

    [Fact]
    public async Task HostNameMismatch_Ignored_StillRejectsAnUntrustedChain()
    {
        // Ignoring the hostname must not turn into ignoring the chain.
        using var bundle = new TrustBundle(_unrelatedRoot);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle,
            ignoreNameMismatch: true, targetHost: WrongHostName);

        HandshakeAssert.RejectedByValidator(result,
            "Ignoring the name mismatch must not bypass trust anchor validation.");
    }

    [Fact]
    public async Task DefaultCertificates_RejectAPrivateChain_AndSurviveTheAttempt()
    {
        // The Capella default path. A private chain must not validate against it, and the shared static
        // default certificate must still be usable afterwards, since it is exactly the kind of long-lived
        // instance NCBC-4120 was about.
        var defaults = new X509Certificate2Collection(CertificateFactory.DefaultCertificates.ToArray());
        var validator = CreateValidator(defaults);
        var thumbprintBefore = CertificateFactory.DefaultCertificates[0].Thumbprint;

        var result = await TlsLoopback.RunAsync(
            _leaf, new[] { _intermediate }, validator.Validate, LeafDnsName, _output);

        HandshakeAssert.RejectedByValidator(result,
            "A privately issued chain must not validate against the bundled Capella defaults.");
        Assert.Equal(thumbprintBefore, CertificateFactory.DefaultCertificates[0].Thumbprint);
        Assert.NotEmpty(CertificateFactory.DefaultCertificates[0].RawData);
    }

    [Fact]
    public void NoCertificatePresented_ReturnsFalse()
    {
        using var bundle = new TrustBundle(_root);
        var validator = CreateValidator(bundle.Certificates);

        var accepted = validator.Validate(new object(), null, null, SslPolicyErrors.RemoteCertificateNotAvailable);

        Assert.False(accepted, "A handshake with no server certificate must be rejected.");
    }

    [Fact]
    public void NoErrors_Accepts_WithoutBuildingAChain()
    {
        // The OS trust store already approved the chain, so the configured anchors are not consulted at all.
        // An unrelated anchor proves no chain build took place.
        using var bundle = new TrustBundle(_unrelatedRoot);
        var validator = CreateValidator(bundle.Certificates);
        using var chain = new X509Chain();

        var accepted = validator.Validate(new object(), _leaf, chain, SslPolicyErrors.None);

        Assert.True(accepted, "A chain the OS accepted must be accepted without a rebuild.");
    }

    [Fact]
    public void OnlyNameMismatch_Ignored_Accepts()
    {
        // Masking the name mismatch leaves no errors, so the OS verdict stands and no chain is built.
        using var bundle = new TrustBundle(_unrelatedRoot);
        var validator = CreateValidator(bundle.Certificates, ignoreNameMismatch: true);
        using var chain = new X509Chain();

        var accepted = validator.Validate(new object(), _leaf, chain, SslPolicyErrors.RemoteCertificateNameMismatch);

        Assert.True(accepted, "With the name mismatch ignored and no other error, the OS verdict stands.");
    }

    [Fact]
    public void ChainErrorsAndNameMismatch_Ignored_StillValidatesChain()
    {
        // Masking the name mismatch must leave the chain errors to be resolved against the configured
        // anchors, not waved through.
        const SslPolicyErrors errors =
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

        using var untrustedBundle = new TrustBundle(_unrelatedRoot);
        var rejecting = CreateValidator(untrustedBundle.Certificates, ignoreNameMismatch: true);
        Assert.False(DirectChain.Validate(rejecting.Validate, _leaf, new[] { _intermediate }, errors),
            "An untrusted chain must still be rejected when the name mismatch is ignored.");

        using var trustedBundle = new TrustBundle(_root);
        var accepting = CreateValidator(trustedBundle.Certificates, ignoreNameMismatch: true);
        Assert.True(DirectChain.Validate(accepting.Validate, _leaf, new[] { _intermediate }, errors),
            "A trusted chain must be accepted once the name mismatch is ignored.");
    }

    [Fact]
    public void UnexpectedError_Rejects()
    {
        // Anything other than a plain chain error is not something the configured anchors can fix.
        using var bundle = new TrustBundle(_root);
        var validator = CreateValidator(bundle.Certificates);
        using var chain = new X509Chain();

        var accepted = validator.Validate(new object(), _leaf, chain, SslPolicyErrors.RemoteCertificateNotAvailable);

        Assert.False(accepted, "An error the trust anchors cannot explain must be rejected.");
    }

    [Fact]
    public void GivenChain_IsNotModified()
    {
        // The validator builds its own chain, so the one SslStream owns must come back exactly as it went in.
        using var bundle = new TrustBundle(_root);
        var validator = CreateValidator(bundle.Certificates);
        using var wireCopy = TlsTestPki.CopyOf(_intermediate);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(wireCopy);

        var extraStoreCountBefore = chain.ChainPolicy.ExtraStore.Count;
        var trustModeBefore = chain.ChainPolicy.TrustMode;
        var customTrustStoreCountBefore = chain.ChainPolicy.CustomTrustStore.Count;

        var accepted = validator.Validate(new object(), _leaf, chain, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.True(accepted, "The leaf chains to the trusted root through the wire intermediate.");
        Assert.Equal(extraStoreCountBefore, chain.ChainPolicy.ExtraStore.Count);
        Assert.Equal(trustModeBefore, chain.ChainPolicy.TrustMode);
        Assert.Equal(customTrustStoreCountBefore, chain.ChainPolicy.CustomTrustStore.Count);
        Assert.NotEmpty(wireCopy.RawData);
    }

    [Fact]
    public async Task RejectedValidation_LogsChainStatus()
    {
        // The failure and success log branches used to share one condition on debug being enabled, so a
        // rejection with debug logging off was reported as a success.
        using var bundle = new TrustBundle(_unrelatedRoot);
        var logger = new RecordingLogger(debugEnabled: false);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle, logger: logger);

        HandshakeAssert.RejectedByValidator(result, "An unrelated root must not validate the leaf's chain.");
        Assert.Contains(logger.Messages, m => m.Contains("rejected"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("accepted"));
    }

    private ServerCertificateValidator CreateValidator(
        X509Certificate2Collection trustedCertificates, bool ignoreNameMismatch = false, ILogger? logger = null)
    {
        var redactor = new Mock<IRedactor>();
        redactor.Setup(r => r.SystemData(It.IsAny<object>())).Returns<object?>(message => message!);

        return new ServerCertificateValidator(
            trustedCertificates,
            ignoreNameMismatch,
            X509RevocationMode.NoCheck,
            logger ?? NullLogger.Instance,
            redactor.Object);
    }

    private Task<HandshakeResult> Handshake(
        X509Certificate2 serverLeaf,
        X509Certificate2[] wireExtras,
        TrustBundle bundle,
        bool ignoreNameMismatch = false,
        string targetHost = LeafDnsName,
        ILogger? logger = null)
    {
        var validator = CreateValidator(bundle.Certificates, ignoreNameMismatch, logger);
        return TlsLoopback.RunAsync(serverLeaf, wireExtras, validator.Validate, targetHost, _output);
    }

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
