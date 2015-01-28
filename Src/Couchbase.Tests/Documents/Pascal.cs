using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Tests.Documents
{
    /// <summary>
    /// 'Pascal' POCO for testing PascalCase related conversions.
    /// </summary>
    class Pascal
    {
        public string SomeProperty { get; set; }

        public int SomeIntProperty { get; set; }

        public bool HasPascalCase { get; set; }
    }
}
