using Couchbase.Core.Compatibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Couchbase.Core.IO.Authentication.X509
{
    [InterfaceStability(Level.Volatile)]
    public class PredefinedCertificateFactory : ICertificateFactory
    {
        private readonly X509Certificate2[] _certs;

        public PredefinedCertificateFactory(params X509Certificate2[] certs)
        {
            _certs = certs;
        }

        public PredefinedCertificateFactory(IEnumerable<X509Certificate2> certs)
            : this(certs.ToArray())
        { }

        public PredefinedCertificateFactory(X509Certificate2Collection certs)
        {
            _certs = certs.Cast<X509Certificate2>().ToArray();
        }

        public X509Certificate2Collection GetCertificates() => new X509Certificate2Collection(_certs);
    }
}
