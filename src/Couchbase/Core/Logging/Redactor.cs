using Couchbase.Utils;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Represents a logging argument that is redactable.
    /// </summary>
    /// <remarks>
    /// Forwards redaction to a <see cref="TypedRedactor"/>.
    /// </remarks>
    internal class Redactor : IRedactor
    {
        private readonly TypedRedactor _typedRedactor;

        public Redactor(TypedRedactor typedRedactor)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (typedRedactor == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(typedRedactor));
            }

            _typedRedactor = typedRedactor;
        }

        public RedactionLevel RedactionLevel => _typedRedactor.RedactionLevel;

        [return: NotNullIfNotNull("message")]
        public object? UserData(object? message) => message is not null ? _typedRedactor.UserData(message) : null;

        [return: NotNullIfNotNull("message")]
        public object? MetaData(object? message) => message is not null ? _typedRedactor.MetaData(message) : null;

        [return: NotNullIfNotNull("message")]
        public object? SystemData(object? message) => message is not null ? _typedRedactor.SystemData(message) : null;
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
