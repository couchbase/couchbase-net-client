namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Specifies the level of log redaction.
    /// </summary>
    public enum RedactionLevel
    {
        /// <summary>
        /// No redaction is performed; this is the default.
        /// </summary>
        None,

        /// <summary>
        /// Only user data is redacted; system and metadata are not.
        /// </summary>
        Partial,

        /// <summary>
        /// User, system, and metadata are redacted.
        /// </summary>
        Full
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
