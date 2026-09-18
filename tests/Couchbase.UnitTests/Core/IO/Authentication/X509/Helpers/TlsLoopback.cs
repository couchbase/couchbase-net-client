#if NET6_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;

/// <summary>
/// Drives a validation callback over a real loopback TLS handshake.
/// </summary>
/// <remarks>
/// This is the only way to exercise the SDK's validation the way production does: SslStream receives the
/// server-presented chain, pre-populates the X509Chain handed to the callback, and the callback decides.
/// The harness deliberately does not build its own chain, so the connection result IS the SDK's verdict.
/// </remarks>
internal static class TlsLoopback
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Serves <paramref name="serverLeaf"/> plus <paramref name="wireExtras"/> on loopback and connects a
    /// client that validates with <paramref name="validator"/>.
    /// </summary>
    /// <remarks>
    /// Copies are used for everything handed to SslStream, so the caller's certificates survive the
    /// handshake. Certificates captured by <paramref name="validator"/> are left untouched and remain the
    /// caller's to dispose.
    /// </remarks>
    public static async Task<HandshakeResult> RunAsync(
        X509Certificate2 serverLeaf,
        IEnumerable<X509Certificate2> wireExtras,
        RemoteCertificateValidationCallback validator,
        string targetHost,
        ITestOutputHelper? output = null)
    {
        var wireCollection = new X509Certificate2Collection();
        foreach (var extra in wireExtras)
        {
            if (IsSelfSigned(extra))
            {
                throw new InvalidOperationException(
                    $"\"{extra.Subject}\" is self-signed. SslStreamCertificateContext drops self-signed "
                    + "certificates from the chain it serves, so this one would never reach the validator "
                    + "and the test would pass without proving anything. Use DirectChain.Validate to put a "
                    + "root in the ExtraStore.");
            }

            wireCollection.Add(TlsTestPki.CopyOf(extra));
        }

        // The root, if any, is intentionally absent unless the caller passed it as a wire extra.
        var leafCopy = TlsTestPki.CopyOf(serverLeaf);
        var serverContext = SslStreamCertificateContext.Create(leafCopy, wireCollection, offline: true);

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
                // The client rejecting the certificate tears down the handshake, so this is expected for
                // negative cases. Kept rather than swallowed, and re-surfaced below only when the client
                // failed for a reason other than a verdict from the validator.
                serverException = ex;
            }
        }, CancellationToken.None);

        var invoked = false;
        bool? verdict = null;
        Exception? validatorException = null;

        bool Recording(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
        {
            invoked = true;
            try
            {
                var result = validator(sender, certificate, chain, errors);
                verdict = result;
                return result;
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
            using var sslClient = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, Recording);

            var accepted = false;
            try
            {
                await sslClient.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions { TargetHost = targetHost }, cts.Token).ConfigureAwait(false);
                accepted = true;
            }
            catch (AuthenticationException ex)
            {
                output?.WriteLine($"Handshake rejected: {ex.Message}");
            }

            if (!invoked)
            {
                throw new InvalidOperationException(
                    "TLS handshake failed before the validation callback was invoked.", serverException);
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

            leafCopy.Dispose();
        }
    }

    private static bool IsSelfSigned(X509Certificate2 cert) =>
        cert.SubjectName.RawData.AsSpan().SequenceEqual(cert.IssuerName.RawData);
}

#endif
