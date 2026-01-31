using System;

namespace Couchbase.Views
{
    /// <summary>
    /// Extension methods for working withe StaleState enumeration.
    /// </summary>
    [Obsolete("The View service has been deprecated use the Query service instead.")]
    internal static class StaleStateExtensions
    {
        /// <summary>
        /// Converts the StaleState value to a lowercase string.
        /// </summary>
        /// <param name="value">The <see cref="StaleState"/> enumeration value to convert to a string.</param>
        /// <returns>The string value of a StaleState enumeration.</returns>
        public static string ToLowerString(this StaleState value)
        {
            var parsed = "false";
            switch (value)
            {
                case StaleState.False:
                    break;
                case StaleState.Ok:
                    parsed = "ok";
                    break;
                case StaleState.UpdateAfter:
                    parsed = "update_after";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
            return parsed;
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
