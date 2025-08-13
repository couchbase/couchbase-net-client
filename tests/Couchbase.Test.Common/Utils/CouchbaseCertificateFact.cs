using System;
using Couchbase.IntegrationTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Couchbase.IntegrationTests.Utils
{
    /// <summary>
    /// A <c>Fact</c> that may be skipped due not having a Certificate File.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class CouchbaseCertificateFact : FactAttribute
    {
        public static Lazy<string> CertPath = new(() =>
            {

                var _settings = new ConfigurationBuilder()
                    .AddJsonFile("config.json")
                    .Build()
                    .GetSection("testSettings")
                    .Get<TestSettings>();
                return _settings.CertificatesFilePath;
            }
        );

        /// <summary>
        /// Gets or sets the list of versions that will be considered as no-skip. Supercedes everything but the Skip parameter itself.
        /// </summary>
        public string[] ExplicitAllow { get; set; }

        /// <summary>
        /// Gets or sets the list of versions that will be skipped. Supercedes MinVersion and MaxVersion.
        /// </summary>
        public string[] ExplicitSkip { get; set; }

        /// <summary>
        /// Gets or sets a value indicating this test should be skipped.  Supercedes all other version checking.
        /// </summary>
        public override string Skip
        {
            get => SkipBasedOnCertPath(base.Skip, CertFilePath);
            set => base.Skip = value;
        }

        /// <summary>
        /// Gets or sets the maximum Version necessary to run the test.
        /// </summary>
        /// <remarks>If it does not parse as a Version, it will be ignored.</remarks>
        public string CertFilePath { get; set; }

        /// <summary>
        /// fp is the arg to
        ///         [CouchbaseCertificateFact(CertFilePath = "required")]
        /// </summary>
        /// <remarks>If it does not parse as a Version, it will be ignored.</remarks>
        internal static string SkipBasedOnCertPath(String baseSkip, String fp)
        {
            var certPath = CertPath.Value;
            if (certPath != null && fp == null)
            {
               // throw new Exception("Certificate path could not be null.");
            }
            return certPath == null && fp != null ? "CertificatePath in config.json is null but required for this test" : baseSkip;
        }
    }
}
