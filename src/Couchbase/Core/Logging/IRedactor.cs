using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// An interface used for redacting specific log information.
    /// </summary>
    public interface IRedactor
    {
        /// <summary>
        /// Redact user data like username, statements, etc
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull("message")]
        object? UserData(object? message);

        /// <summary>
        /// Redact meta data like bucket names, etc
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull("message")]
        object? MetaData(object? message);

        /// <summary>
        /// Redact system data like hostnames, etc.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull("message")]
        object? SystemData(object? message);
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
