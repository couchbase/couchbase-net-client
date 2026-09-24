#if NET7_0_OR_GREATER

using System;
using System.Linq;
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
    public const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
    public const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";
    private const string OrganizationOid = "2.5.4.10";

    /// <summary>
    /// Creates a CA certificate, self-signed when <paramref name="issuer"/> is null.
    /// </summary>
    /// <param name="pathLengthConstraint">
    /// Maximum number of intermediate CAs permitted below this one. Null means unconstrained.
    /// </param>
    /// <param name="ekus">Extended key usages to add. Null means no EKU extension.</param>
    public static X509Certificate2 CreateCa(
        string commonName,
        X509Certificate2? issuer = null,
        int? pathLengthConstraint = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        OidCollection? ekus = null)
    {
        // A random O per hierarchy keeps CA names unique. Windows copies served intermediates into the machine
        // CA store, and same named CAs with other keys break chain building there (dotnet/runtime#99425).
        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName(commonName);
        subject.AddOrganizationName(HierarchyIdOf(issuer));

        var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject.Build(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true,
            hasPathLengthConstraint: pathLengthConstraint.HasValue,
            pathLengthConstraint: pathLengthConstraint ?? 0,
            critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        if (ekus != null)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(ekus, critical: false));
        }
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        // CAs start well in the past so that certificates below them have room to be minted as expired.
        var from = notBefore ?? DateTimeOffset.UtcNow.AddYears(-1);

        if (issuer is null)
        {
            // Self-signed root, keeps the private key so it can sign children.
            return request.CreateSelfSigned(from, notAfter ?? DateTimeOffset.UtcNow.AddYears(10));
        }

        request.CertificateExtensions.Add(AuthorityKeyIdentifierOf(issuer));
        var signed = request.Create(
            issuer, ClampNotBefore(from, issuer), ClampNotAfter(notAfter, issuer), NewSerial());
        return signed.CopyWithPrivateKey(rsa);
    }

    /// <summary>
    /// Creates a TLS server certificate with <paramref name="dnsName"/> as its only SAN, self-signed when
    /// <paramref name="issuer"/> is null.
    /// </summary>
    /// <param name="ekus">Extended key usages to add. Null means serverAuth only.</param>
    public static X509Certificate2 CreateServerLeaf(
        string commonName,
        string dnsName,
        X509Certificate2? issuer = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        OidCollection? ekus = null)
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            ekus ?? new OidCollection { new Oid(ServerAuthOid) }, critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        request.CertificateExtensions.Add(san.Build());

        var from = notBefore ?? DateTimeOffset.UtcNow.AddDays(-1);

        if (issuer is null)
        {
            return ServableBySchannel(request.CreateSelfSigned(from, notAfter ?? DateTimeOffset.UtcNow.AddYears(2)));
        }

        request.CertificateExtensions.Add(AuthorityKeyIdentifierOf(issuer));
        var signed = request.Create(
            issuer, ClampNotBefore(from, issuer), ClampNotAfter(notAfter, issuer), NewSerial());
        return ServableBySchannel(signed.CopyWithPrivateKey(rsa));
    }

    /// <summary>
    /// SChannel refuses to serve TLS with an in memory private key, so on Windows the certificate is
    /// round tripped through PKCS12 to land the key in a key container. The container is removed when
    /// the certificate is disposed. Other platforms serve the in memory key as is.
    /// </summary>
    private static X509Certificate2 ServableBySchannel(X509Certificate2 cert)
    {
        if (!OperatingSystem.IsWindows())
        {
            return cert;
        }

        using (cert)
        {
            var pfx = cert.Export(X509ContentType.Pkcs12);
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(pfx, password: null, X509KeyStorageFlags.Exportable);
#else
            return new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable);
#endif
        }
    }

    /// <summary>
    /// Tells a certificate apart from a same named issuer by key, the way real PKIs do. Without it a
    /// subordinate CA that shares its root's subject name looks self-signed to a name based check.
    /// </summary>
    public static bool IsSelfSigned(X509Certificate2 cert)
    {
        if (!cert.SubjectName.RawData.AsSpan().SequenceEqual(cert.IssuerName.RawData))
        {
            return false;
        }

        var authorityKey = cert.Extensions.OfType<X509AuthorityKeyIdentifierExtension>().FirstOrDefault()?.KeyIdentifier;
        var subjectKey = cert.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault()?.SubjectKeyIdentifierBytes;
        return authorityKey is null || subjectKey is null || authorityKey.Value.Span.SequenceEqual(subjectKey.Value.Span);
    }

    private static string HierarchyIdOf(X509Certificate2? issuer) =>
        issuer?.SubjectName.EnumerateRelativeDistinguishedNames()
            .Where(rdn => !rdn.HasMultipleElements && rdn.GetSingleElementType().Value == OrganizationOid)
            .Select(rdn => rdn.GetSingleElementValue())
            .FirstOrDefault(value => value is not null)
        ?? Guid.NewGuid().ToString("N")[..8];

    private static X509AuthorityKeyIdentifierExtension AuthorityKeyIdentifierOf(X509Certificate2 issuer) =>
        X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, includeKeyIdentifier: true, includeIssuerAndSerial: false);

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
