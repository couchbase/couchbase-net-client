using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase
{
    internal interface IHttpClientLocator
    {
        IHttpClient Locate();
    }
}