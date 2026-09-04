#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Redaction helpers for error contexts, which store plain strings rather than
    /// <see cref="Redacted{T}"/>. These mirror the like-named methods on <see cref="TypedRedactor"/>
    /// so that every error-context assignment reads the same way regardless of which redactor
    /// abstraction the enclosing class holds.
    /// </summary>
    internal static class RedactorExtensions
    {
        public static string? UserDataString(this IRedactor redactor, string? value) =>
            string.IsNullOrEmpty(value) ? value : redactor.UserData(value)?.ToString();

        public static string? MetaDataString(this IRedactor redactor, string? value) =>
            string.IsNullOrEmpty(value) ? value : redactor.MetaData(value)?.ToString();

        public static string? SystemDataString(this IRedactor redactor, string? value) =>
            string.IsNullOrEmpty(value) ? value : redactor.SystemData(value)?.ToString();
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
