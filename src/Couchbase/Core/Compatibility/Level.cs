namespace Couchbase.Core.Compatibility
{
    /// <summary>
    /// Designates the interface stability of a given API; how likely the interface is to change or be removed entirely.
    /// </summary>
    public enum Level
    {
        /// <summary>
        /// This stability level is used to indicate the most stable interfaces that are guaranteed to be
        /// supported and remain stable between SDK versions.
        /// </summary>
        Committed = 0x00,

        /// <summary>
        /// This level is used to indicate APIs that are unlikely to change, but may still change as final
        /// consensus on their behavior has not yet been reached. Uncommitted APIs usually end up becoming
        /// stable APIs.
        /// </summary>
        Uncommitted = 0x01,

        /// <summary>
        /// This level is used to indicate experimental APIs that are still in flux and may likely be changed.
        /// It may also be used to indicate inherently private APIs that may be exposed, but "YMMV"
        /// (your mileage may vary) principles apply. Volatile APIs typically end up being promoted to
        /// Uncommitted after undergoing some modifications.
        /// </summary>
        Volatile = 0x02
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
