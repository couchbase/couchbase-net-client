#if NET6_0_OR_GREATER

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Connections;

/// <summary>
/// Drives <see cref="CallbackCreator"/>, the callback actually installed on production connections, over a
/// loopback TLS handshake.
/// </summary>
/// <remarks>
/// Tests that call the inner chain validator directly bypass this type, and with it the hostname gate and
/// the choice between user-supplied trust anchors and the bundled Capella defaults. Those branches are only
/// reachable here.
/// </remarks>
public sealed class CallbackCreatorHandshakeTests : IDisposable
{
    private const string LeafDnsName = "localhost";
    private const string WrongHostName = "wrong.example.com";

    private readonly ITestOutputHelper _output;
    private readonly X509Certificate2 _root;
    private readonly X509Certificate2 _intermediate;
    private readonly X509Certificate2 _leaf;

    public CallbackCreatorHandshakeTests(ITestOutputHelper output)
    {
        _output = output;

        _root = TlsTestPki.CreateCa("Composed Root CA");
        _intermediate = TlsTestPki.CreateCa("Composed Intermediate CA", issuer: _root);
        _leaf = TlsTestPki.CreateServerLeaf("Composed Leaf", LeafDnsName, issuer: _intermediate);
    }

    [Fact]
    public async Task ValidChain_MatchingHostName_Accepts()
    {
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(bundle, ignoreNameMismatch: false, targetHost: LeafDnsName);

        HandshakeAssert.Accepted(result, "A valid chain served under its own SAN should be accepted.");
    }

    [Fact]
    public async Task HostNameMismatch_NotIgnored_Rejects()
    {
        // The chain itself is fine, so only the hostname gate can reject here.
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(bundle, ignoreNameMismatch: false, targetHost: WrongHostName);

        HandshakeAssert.RejectedByValidator(result,
            "A certificate that does not match the requested host must be rejected by default.");
    }

    [Fact]
    public async Task HostNameMismatch_Ignored_Accepts()
    {
        // KvIgnoreRemoteCertificateNameMismatch / HttpIgnoreRemoteCertificateNameMismatch opt out of the
        // hostname check only. The chain is still validated.
        using var bundle = new TrustBundle(_root);

        var result = await Handshake(bundle, ignoreNameMismatch: true, targetHost: WrongHostName);

        HandshakeAssert.Accepted(result,
            "With the name mismatch ignored, a valid chain should be accepted despite the wrong host.");
    }

    [Fact]
    public async Task HostNameMismatch_Ignored_StillRejectsAnUntrustedChain()
    {
        // Ignoring the hostname must not turn into ignoring the chain.
        using var unrelatedRoot = TlsTestPki.CreateCa("Composed Unrelated Root CA");
        using var bundle = new TrustBundle(unrelatedRoot);

        var result = await Handshake(bundle, ignoreNameMismatch: true, targetHost: WrongHostName);

        HandshakeAssert.RejectedByValidator(result,
            "Ignoring the name mismatch must not bypass trust anchor validation.");
    }

    [Fact]
    public async Task NoConfiguredTrustAnchors_FallsBackToDefaults_AndRejectsAPrivateChain()
    {
        // With no trust anchors configured the callback validates against the bundled Capella defaults,
        // which must not accept a privately issued cluster certificate.
        var result = await Handshake(bundle: null, ignoreNameMismatch: false, targetHost: LeafDnsName);

        HandshakeAssert.RejectedByValidator(result,
            "Falling back to the Capella defaults must not accept an unrelated private chain.");
    }

    private Task<HandshakeResult> Handshake(TrustBundle? bundle, bool ignoreNameMismatch, string targetHost)
    {
        var callbackCreator = new CallbackCreator(
            ignoreNameMismatch,
            NullLogger<object>.Instance,
            new Mock<IRedactor>().Object,
            bundle?.Certificates);

        return TlsLoopback.RunAsync(
            _leaf, new[] { _intermediate }, callbackCreator.Callback, targetHost, _output);
    }

    public void Dispose()
    {
        _root.Dispose();
        _intermediate.Dispose();
        _leaf.Dispose();
    }
}

#endif
