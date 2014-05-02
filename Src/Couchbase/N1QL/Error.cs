using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Represents additional information returned from a N1QL query when an error has occurred.
    /// </summary>
    public struct Error
    {
        public string Caller { get; set; }

        public int Code { get; set; }

        public string Cause { get; set; }

        public string Key { get; set; }

        public string Message { get; set; }
    }
}
