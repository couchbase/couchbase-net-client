using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Client.Providers
{
    public sealed class BucketElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new BucketElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var elm = element as BucketElement;
            if (elm == null)
            {
                throw new InvalidCastException("element");
            }
            return elm.Name;
        }
    }
}
