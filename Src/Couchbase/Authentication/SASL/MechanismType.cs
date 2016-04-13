namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// Supported SASL authentication types supported by Couchbase Server.
    /// </summary>
    internal static class MechanismType
    {
        /// <summary>
        /// The username and password will be sent encrypted using salted Sha512 and will not be human-readable on the wire.
        /// </summary>
        public static string ScramSha512 = "SCRAM-SHA512";

        /// <summary>
        /// The username and password will be sent encrypted using salted Sha256 and will not be human-readable on the wire.
        /// </summary>
        public static string ScramSha256 = "SCRAM-SHA256";

        /// <summary>
        /// The username and password will be sent encrypted using salted Sha1 and will not be human-readable on the wire.
        /// </summary>
        public static string ScramSha1 = "SCRAM-SHA1";

        /// <summary>
        /// The username and password will be sent encrypted using CramMD5 and will not be human-readable on the wire.
        /// </summary>
        public static string CramMd5 = "CRAM-MD5";

        /// <summary>
        /// The username and password will be sent using human-readable plain text.
        /// </summary>
        public static string Plain = "PLAIN";
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
