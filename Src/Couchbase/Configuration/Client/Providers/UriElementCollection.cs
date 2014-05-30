using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Client.Providers
{
    public sealed class UriElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new UriElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var elm = element as UriElement;
            if (elm == null)
            {
                throw new InvalidCastException("element");
            }
            return elm.Uri;
        }
    }
}
