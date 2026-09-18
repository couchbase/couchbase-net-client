using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.Compatibility;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication.X509
{
    /// <summary>
    /// Factory class for creating <see cref="ICertificateFactory"/> instance that can be assigned to the <see cref="CertificateFactory"></see> property.
    /// </summary>
    public static class CertificateFactory
    {
        /// <summary>
        /// Creates an <see cref="ICertificateFactory"/> given a path and password to a .pfx certificate.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static ICertificateFactory GetCertificatesByPathAndPassword(string path, string password)
        {
            path = path ?? throw new ArgumentNullException(nameof(path));
            password = password ?? throw new ArgumentNullException(nameof(password));

            return new CertificatePathFactory(path, password);
        }

        /// <summary>
        /// Creates an <see cref="Func{X509Certificate2Collection}"/> given <see cref="CertificateStoreSearchCriteria"/> to find a .pfx
        /// certificate in the Windows Cert Store.
        /// </summary>
        /// <param name="searchCriteria"></param>
        /// <returns></returns>
        public static ICertificateFactory GetCertificatesFromStore(CertificateStoreSearchCriteria searchCriteria)
        {
            searchCriteria = searchCriteria ?? throw new ArgumentNullException(nameof(searchCriteria));

            return new CertificateStoreFactory(searchCriteria);
        }

        /// <summary>
        /// Create Creates an <see cref="Func{X509Certificate2Collection}"/> from certificates you have predefined.
        /// </summary>
        /// <param name="certs">The pre-defined certificates you wish to use.</param>
        /// <returns>A certificate factory with predefined certificates.</returns>
        public static ICertificateFactory FromCertificates(params X509Certificate2[] certs) => new PredefinedCertificateFactory(certs);

        /// <summary>
        /// The certificate (in PEM format) to use by default for connecting to *.cloud.couchbase.com.
        /// </summary>
        /// <remarks>
        /// This in-memory certificate does not work on .NET Framework (legacy) clients.
        /// </remarks>
        internal static ReadOnlySpan<byte> CapellaCaCertPem =>
            """
            -----BEGIN CERTIFICATE-----
            MIIDFTCCAf2gAwIBAgIRANLVkgOvtaXiQJi0V6qeNtswDQYJKoZIhvcNAQELBQAw
            JDESMBAGA1UECgwJQ291Y2hiYXNlMQ4wDAYDVQQLDAVDbG91ZDAeFw0xOTEyMDYy
            MjEyNTlaFw0yOTEyMDYyMzEyNTlaMCQxEjAQBgNVBAoMCUNvdWNoYmFzZTEOMAwG
            A1UECwwFQ2xvdWQwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCfvOIi
            enG4Dp+hJu9asdxEMRmH70hDyMXv5ZjBhbo39a42QwR59y/rC/sahLLQuNwqif85
            Fod1DkqgO6Ng3vecSAwyYVkj5NKdycQu5tzsZkghlpSDAyI0xlIPSQjoORA/pCOU
            WOpymA9dOjC1bo6rDyw0yWP2nFAI/KA4Z806XeqLREuB7292UnSsgFs4/5lqeil6
            rL3ooAw/i0uxr/TQSaxi1l8t4iMt4/gU+W52+8Yol0JbXBTFX6itg62ppb/Eugmn
            mQRMgL67ccZs7cJ9/A0wlXencX2ohZQOR3mtknfol3FH4+glQFn27Q4xBCzVkY9j
            KQ20T1LgmGSngBInAgMBAAGjQjBAMA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYE
            FJQOBPvrkU2In1Sjoxt97Xy8+cKNMA4GA1UdDwEB/wQEAwIBhjANBgkqhkiG9w0B
            AQsFAAOCAQEARgM6XwcXPLSpFdSf0w8PtpNGehmdWijPM3wHb7WZiS47iNen3oq8
            m2mm6V3Z57wbboPpfI+VEzbhiDcFfVnK1CXMC0tkF3fnOG1BDDvwt4jU95vBiNjY
            xdzlTP/Z+qr0cnVbGBSZ+fbXstSiRaaAVcqQyv3BRvBadKBkCyPwo+7svQnScQ5P
            Js7HEHKVms5tZTgKIw1fbmgR2XHleah1AcANB+MAPBCcTgqurqr5G7W2aPSBLLGA
            fRIiVzm7VFLc7kWbp7ENH39HVG6TZzKnfl9zJYeiklo5vQQhGSMhzBsO70z4RRzi
            DPFAN/4qZAgD5q3AFNIq2WWADFQGSwVJhg==
            -----END CERTIFICATE-----
            """u8;

        /// <summary>
        /// The certificate to use by default for connecting to *.cloud.couchbase.com.
        /// </summary>
        /// <remarks>
        /// This in-memory certificate does not work on .NET Framework (legacy) clients.
        /// </remarks>
        [InterfaceStability(Level.Volatile)]
        #if NETSTANDARD2_0 || NETSTANDARD2_1 || NET8_0
         internal static readonly X509Certificate2 CapellaCaCert = new X509Certificate2(
              rawData: CapellaCaCertPem.ToArray(),
              password: (string?)null);
        #else
        internal static X509Certificate2 CapellaCaCert =
            X509CertificateLoader.LoadCertificate(CapellaCaCertPem);
        #endif

        /// <summary>
        /// Default CA Certificates included with the SDK.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        public static readonly IReadOnlyList<X509Certificate2> DefaultCertificates = new List<X509Certificate2>()
        {
            CapellaCaCert,
        };

        [InterfaceStability(Level.Volatile)]
        public static SslPolicyErrors WithoutNameMismatch(SslPolicyErrors errors, ILogger? logger = null)
        {
            if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != SslPolicyErrors.None)
            {
                // clear the name mismatch
                logger?.LogDebug("Clearing certificate name mismatch error.");
                errors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
            }

            return errors;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2020 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
#endregion
