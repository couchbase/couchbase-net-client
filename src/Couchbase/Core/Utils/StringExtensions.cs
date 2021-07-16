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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
