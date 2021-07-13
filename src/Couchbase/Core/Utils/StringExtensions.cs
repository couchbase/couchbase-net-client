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

        /// <summary>
        /// Adds back ticks to the beginning and end of a string if they do not already exist.
        /// </summary>
        /// <param name="value">A value such as a bucket or scope name.</param>
        /// <returns>The original value escaped with back ticks.</returns>
        public static string EscapeIfRequired(this string value)
        {
            const string backtick = "`";
            if (!value.StartsWith(backtick))
            {
                value = backtick + value;
            }

            if (!value.EndsWith(backtick))
            {
                value = value + backtick;
            }

            return value;
        }
    }
}
