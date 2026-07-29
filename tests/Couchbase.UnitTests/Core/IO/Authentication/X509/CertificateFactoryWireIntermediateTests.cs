#if NET6_0_OR_GREATER

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509;

/// <summary>
/// Exercises <see cref="CertificateFactory.GetValidatorWithPredefinedCertificates"/> against real PKI
/// hierarchies over a loopback TLS handshake.
/// </summary>
/// <remarks>
/// Regression coverage for the "honour wire intermediates" fix (NCBC-4216): a leaf that depends on a
/// server-presented intermediate must validate when only the root is in the trust bundle. Also pins the
/// certificate-ownership invariant from NCBC-4120, that a trust bundle survives being validated against.
/// </remarks>
public sealed class CertificateFactoryWireIntermediateTests : IDisposable
{
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

        _root = TlsTestPki.CreateCa("Test Wire Root CA");
        _intermediate = TlsTestPki.CreateCa("Test Wire Intermediate CA", issuer: _root);
        _leaf = TlsTestPki.CreateServerLeaf("Test Wire Leaf", LeafDnsName, issuer: _intermediate);
        _unrelatedRoot = TlsTestPki.CreateCa("Unrelated Root CA");
        _selfSignedLeaf = TlsTestPki.CreateServerLeaf("Self Signed Node", LeafDnsName);
    }

    [Fact]
    public async Task RootOnly_InTrustBundle_Accepts_BecauseWireIntermediateIsHonoured()
    {
        // Only the root is trusted. The leaf can only validate if the SDK honours the intermediate the
        // server presented on the wire. Without that, the retry rebuilds the chain from the root-only
        // bundle, produces a PartialChain, and rejects a perfectly valid leaf.
        using var bundle = new TrustBundle(_root);
        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.Accepted(result,
            "Validator should accept a leaf signed by a wire-presented intermediate when the root is trusted.");
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
    public async Task ServerAlsoSendsRoot_BundleHasRootAndIntermediate_Accepts()
    {
        // A server that sends its whole chain including the root, validated against a multi-entry bundle.
        using var bundle = new TrustBundle(_root, _intermediate);

        var result = await Handshake(_leaf, new[] { _intermediate, _root }, bundle);

        HandshakeAssert.Accepted(result,
            "A server-sent root plus a bundle holding the real anchor should validate.");
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
        // Only the intermediate is in the trust bundle (no root). .NET's CustomRootTrust honours only
        // self-signed certificates in CustomTrustStore as anchors, so a non-self-signed intermediate is not
        // an anchor and its own issuer is untrusted. Proves the validator is not accepting anything.
        using var bundle = new TrustBundle(_intermediate);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "Validator must reject when only the (non-anchor) intermediate is trusted and the root is absent.");
    }

    [Fact]
    public async Task UnrelatedRoot_InTrustBundle_Rejects_TrustAnchorsAreEnforced()
    {
        using var bundle = new TrustBundle(_unrelatedRoot);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "Validator must reject when the trust bundle contains only an unrelated root.");
    }

    [Fact]
    public async Task WireCertificatesNeverBecomeTrustAnchors()
    {
        // The whole chain is attacker-supplied on the wire and the bundle is unrelated. If wire
        // certificates were promoted into the trust store this would be accepted.
        using var rogueRoot = TlsTestPki.CreateCa("Rogue Root CA");
        using var rogueIntermediate = TlsTestPki.CreateCa("Rogue Intermediate CA", issuer: rogueRoot);
        using var rogueLeaf = TlsTestPki.CreateServerLeaf("Rogue Leaf", LeafDnsName, issuer: rogueIntermediate);
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(rogueLeaf, new[] { rogueIntermediate, rogueRoot }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "A fully server-supplied chain must not validate against an unrelated trust bundle.");
    }

    [Fact]
    public async Task ExpiredLeaf_Rejects()
    {
        // Also guards the retry gate: an expired leaf reports NotTimeValid alongside UntrustedRoot, and the
        // status flags must be matched as flags rather than by comparing one entry.
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
        // The leaf cannot outlive its issuer, so it expires with it. The point is that an expired CA in the
        // path is rejected rather than skipped.
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
    public async Task SelfSignedLeaf_InTrustBundle_Accepts_ExistingUseCaseStillWorks()
    {
        // The node serves a self-signed certificate and the user pins that exact certificate. It is its own
        // trust anchor, so CustomRootTrust accepts it.
        using var bundle = new TrustBundle(_selfSignedLeaf);

        var result = await Handshake(_selfSignedLeaf, Array.Empty<X509Certificate2>(), bundle);

        HandshakeAssert.Accepted(result,
            "Validator should accept when the presented self-signed leaf is itself in the trust bundle.");
    }

    [Fact]
    public async Task DifferentRealCa_NotInChain_Rejects()
    {
        // A real CA that did not sign the leaf. Merely having "a" CA in the bundle must not be enough.
        using var otherRoot = TlsTestPki.CreateCa("Other Real Root CA");
        using var bundle = new TrustBundle(otherRoot);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "A real-but-unrelated CA in the trust bundle must not validate the leaf's chain.");
    }

    [Fact]
    public async Task CaIssuedLeafPinnedAlone_Rejects_DocumentsCustomRootTrustLimitation()
    {
        // Observation, not a regression: pinning only a CA-issued (non-self-signed) leaf does not work,
        // because CustomRootTrust will not treat it as an anchor and its issuer chain is untrusted. Distinct
        // from SelfSignedLeaf_InTrustBundle_Accepts, where the leaf IS a self-signed anchor. Captured so the
        // difference stays visible rather than being rediscovered.
        using var bundle = new TrustBundle(_leaf);

        var result = await Handshake(_leaf, new[] { _intermediate }, bundle);

        HandshakeAssert.RejectedByValidator(result,
            "Pinning only a CA-issued (non-self-signed) leaf is not honoured by CustomRootTrust.");
    }

    [Fact]
    public async Task DefaultCertificates_RejectAPrivateChain_AndSurviveTheAttempt()
    {
        // The Capella default path had no coverage at all. A private chain must not validate against it,
        // and the shared static default certificate must still be usable afterwards, since it is exactly
        // the kind of long-lived instance NCBC-4120 was about.
        var validator = CertificateFactory.GetValidatorWithDefaultCertificates(logger: null, redactor: null);
        var thumbprintBefore = CertificateFactory.DefaultCertificates[0].Thumbprint;

        var result = await TlsLoopback.RunAsync(_leaf, new[] { _intermediate }, validator, LeafDnsName, _output);

        HandshakeAssert.RejectedByValidator(result,
            "A privately issued chain must not validate against the bundled Capella defaults.");
        Assert.Equal(thumbprintBefore, CertificateFactory.DefaultCertificates[0].Thumbprint);
        Assert.NotEmpty(CertificateFactory.DefaultCertificates[0].RawData);
    }

    private Task<HandshakeResult> Handshake(
        X509Certificate2 serverLeaf, X509Certificate2[] wireExtras, TrustBundle bundle)
    {
        var validator = CertificateFactory.GetValidatorWithPredefinedCertificates(
            bundle.Certificates, logger: null, redactor: null);
        return TlsLoopback.RunAsync(serverLeaf, wireExtras, validator, LeafDnsName, _output);
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
