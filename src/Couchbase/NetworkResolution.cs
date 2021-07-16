namespace Couchbase
{
    /// <summary>
    /// Specifies the network resolution strategy to use for alternative network; used in some container
    /// environments where there maybe internal and external addresses for connecting.
    /// </summary>
    public static class NetworkResolution
    {
        /// <summary>
        /// Alternative addresses will be used if available. The default.
        /// </summary>
        public const string Auto ="auto";

        /// <summary>
        /// Do not use alternative addresses. Uses the internal addresses.
        /// </summary>
        public const string Default = "default";

        /// <summary>
        /// Use alternative addresses.
        /// </summary>
        public const string External = "external";
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
