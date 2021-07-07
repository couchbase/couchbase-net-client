using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Utils
{
    public static class StringExtensions
    {
        public static string ToHexString(this uint opaque)
        {
            const string hexPrefix = "0x", hexFormat = "x";
            return string.Join(hexPrefix, opaque.ToString(hexFormat));
        }
    }
}
