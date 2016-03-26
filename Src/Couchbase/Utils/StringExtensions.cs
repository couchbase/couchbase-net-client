using System;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Text;
using Couchbase.N1QL;

namespace Couchbase.Utils
{
    internal static class StringExtensions
    {

        /// <summary>
        /// Converts a <see cref="System.String"/> to an <see cref="System.Enum"/>. Assumes that
        /// the conversion is possible; e.g. the <see cref="value"/> field must match an enum name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        /// <exception cref="InvalidEnumArgumentException">Thrown if the conversion cannot be made.</exception>
        public static T ToEnum<T>(this string value) where T : struct
        {
            T result;
            if (!Enum.TryParse(value, true, out result))
            {
                throw new InvalidEnumArgumentException();
            }
            return result;
        }

        public static string EncodeParameter(this object parameter)
        {
            return Uri.EscapeDataString(JsonConvert.SerializeObject(parameter));
        }

        /// <summary>
        /// Escape's a string with a N1QL delimiter - the backtick.
        /// </summary>
        /// <param name="theString">The string.</param>
        /// <returns></returns>
        public static string N1QlEscape(this string theString)
        {
            if (!theString.StartsWith("`", StringComparison.OrdinalIgnoreCase))
            {
                theString = theString.Insert(0, "`");
            }
            if (!theString.EndsWith("`", StringComparison.OrdinalIgnoreCase))
            {
                theString = theString.Insert(theString.Length, "`");
            }

            return theString;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion