using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    internal static class BoolExtensions
    {
        public static string ToLowerString(this bool? value)
        {
            return ToLowerString(value.HasValue && value.Value);
        }

        public static string ToLowerString(this bool value)
        {
            return value ? "true" : "false";
        }
    }
}
