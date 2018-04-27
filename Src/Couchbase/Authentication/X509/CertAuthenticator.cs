using System;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Configuration.Client;
using Couchbase.Utils;

namespace Couchbase.Authentication.X509
{
    public class CertAuthenticator : IAuthenticator
    {
        private ClientConfiguration _configuration;

        public CertAuthenticator()
        { }

        public CertAuthenticator(string certificatePath, string certificatePassword)
            : this(new PathAndPasswordOptions
        {
            Password = certificatePassword,
            Path = certificatePath
        }) {}

        public CertAuthenticator(PathAndPasswordOptions options)
        {
            CertificateFunc = CertificateFactory.GetCertificatesByPathAndPassword(options);
        }

        public CertAuthenticator(CertificateStoreOptions options)
        {
            CertificateFunc = CertificateFactory.GetCertificatesFromStore(options);
        }

        public AuthenticatorType AuthenticatorType => AuthenticatorType.Certificate;

        internal Func<X509Certificate2Collection> CertificateFunc { get; set; }

        internal ClientConfiguration Configuration
        {
            get => _configuration;
            set
            {
                if (CertificateFunc != null)
                {
                    value.CertificateFactory = CertificateFunc;
                }
                _configuration = value;
                _configuration.EnableCertificateAuthentication = true;
            }
        }

        public void Validate()
        {
            if (Configuration.EnableCertificateAuthentication)
            {
                if (Configuration.CertificateFactory == null)
                {
                    throw new ArgumentException(ExceptionUtil.NoCertificateFactoryDefined);
                }
            }
        }
    }
}
