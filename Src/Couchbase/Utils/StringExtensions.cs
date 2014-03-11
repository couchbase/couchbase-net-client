using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    internal static class StringExtensions
    {
        public static T ToEnum<T>(this string value) where T : struct 
        {
            T result;
            if (!Enum.TryParse(value, true, out result))
            {
                throw new InvalidEnumArgumentException();
            }
            return result;
        }
    }
}
