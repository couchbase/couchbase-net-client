#if NET6_0_OR_GREATER

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;

/// <summary>
/// Builds throwaway certificate hierarchies for TLS validation tests.
/// </summary>
/// <remarks>
/// Issued certificates keep their private key so they can sign children or serve TLS. Validity defaults
/// to a wide live window; pass <c>notBefore</c>/<c>notAfter</c> explicitly to mint an expired certificate.
/// </remarks>
internal static class TlsTestPki
{
    /// <summary>
    /// Creates a CA certificate, self-signed when <paramref name="issuer"/> is null.
    /// </summary>
    /// <param name="pathLengthConstraint">
    /// Maximum number of intermediate CAs permitted below this one. Null means unconstrained.
    /// </param>
    public static X509Certificate2 CreateCa(
        string commonName,
        X509Certificate2? issuer = null,
        int? pathLengthConstraint = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null)
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true,
            hasPathLengthConstraint: pathLengthConstraint.HasValue,
            pathLengthConstraint: pathLengthConstraint ?? 0,
            critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        // CAs start well in the past so that certificates below them have room to be minted as expired.
        var from = notBefore ?? DateTimeOffset.UtcNow.AddYears(-1);

        if (issuer is null)
        {
            // Self-signed root, keeps the private key so it can sign children.
            return request.CreateSelfSigned(from, notAfter ?? DateTimeOffset.UtcNow.AddYears(10));
        }

        var signed = request.Create(
            issuer, ClampNotBefore(from, issuer), ClampNotAfter(notAfter, issuer), NewSerial());
        return signed.CopyWithPrivateKey(rsa);
    }

    /// <summary>
    /// Creates a TLS server certificate with <paramref name="dnsName"/> as its only SAN, self-signed when
    /// <paramref name="issuer"/> is null.
    /// </summary>
    public static X509Certificate2 CreateServerLeaf(
        string commonName,
        string dnsName,
        X509Certificate2? issuer = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null)
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") /* serverAuth */ }, critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        request.CertificateExtensions.Add(san.Build());

        var from = notBefore ?? DateTimeOffset.UtcNow.AddDays(-1);

        if (issuer is null)
        {
            return request.CreateSelfSigned(from, notAfter ?? DateTimeOffset.UtcNow.AddYears(2));
        }

        var signed = request.Create(
            issuer, ClampNotBefore(from, issuer), ClampNotAfter(notAfter, issuer), NewSerial());
        return signed.CopyWithPrivateKey(rsa);
    }

    /// <summary>
    /// Independent copy of a certificate, so certificates handed to a chain store or to SslStream can be
    /// disposed by the platform without affecting the test's own instances.
    /// </summary>
    public static X509Certificate2 CopyOf(X509Certificate2 cert) => new X509Certificate2(cert);

    /// <summary>
    /// X509 forbids a certificate outliving its issuer, and CertificateRequest.Create rejects it outright.
    /// </summary>
    private static DateTimeOffset ClampNotAfter(DateTimeOffset? requested, X509Certificate2 issuer)
    {
        // The per-call UtcNow drift alone would trip the issuer bound, hence the day of slack.
        var issuerLimit = new DateTimeOffset(issuer.NotAfter).AddDays(-1);
        var desired = requested ?? DateTimeOffset.UtcNow.AddYears(2);
        return desired > issuerLimit ? issuerLimit : desired;
    }

    /// <summary>
    /// Likewise a certificate may not start before its issuer does.
    /// </summary>
    private static DateTimeOffset ClampNotBefore(DateTimeOffset requested, X509Certificate2 issuer)
    {
        var issuerStart = new DateTimeOffset(issuer.NotBefore);
        return requested < issuerStart ? issuerStart : requested;
    }

    private static byte[] NewSerial() => Guid.NewGuid().ToByteArray();
}

#endif
