using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Represents a logging argument that is redactable.
    /// </summary>
    internal class Redactor : IRedactor
    {
        private static readonly string _user = "ud";
        private static readonly string _meta = "md";
        private static readonly string _system = "sd";

        public Redactor(ClusterOptions options)
        {
            RedactionLevel = options.RedactionLevel;
        }

        public RedactionLevel RedactionLevel { get; }

        [return: NotNullIfNotNull("message")]
        public object? UserData(object? message)
        {
            return RedactMessage(message, _user);
        }

        [return: NotNullIfNotNull("message")]
        public object? MetaData(object? message)
        {
            return RedactMessage(message, _meta);
        }

        [return: NotNullIfNotNull("message")]
        public object? SystemData(object? message)
        {
            return RedactMessage(message, _system);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull("message")]
        private object? RedactMessage(object? message, string redactionType)
        {
            switch (RedactionLevel)
            {
                case RedactionLevel.None:
                    return message;

                case RedactionLevel.Full:
                    break;

                case RedactionLevel.Partial:
                    if (!ReferenceEquals(redactionType, _user))
                    {
                        return message;
                    }
                    break;

                default:
                    ThrowArgumentOutOfRangeException(RedactionLevel);
                    break;
            }

            return new Redacted(redactionType, message);
        }

        // Allow RedactMessage to be inlined
        [DoesNotReturn]
        private static void ThrowArgumentOutOfRangeException(RedactionLevel redactionLevel)
        {
            throw new ArgumentOutOfRangeException(Enum.GetName(typeof(RedactionLevel), redactionLevel),
                "Unexpected redaction level: {redactionLevel}");
        }

        /// <summary>
        /// Delays string formatting until actually logging during call to ToString. This avoids the string
        /// formatting cost for disabled log levels, in exchange for an extra Gen 0 heap allocation for log
        /// levels that are enabled. Given the much higher cost of string formatting compared to a small Gen 0
        /// heap allocation, and the fact that most logs are Debug level and disabled, this is faster.
        /// </summary>
        private class Redacted
        {
            private readonly string _redactionType;
            private readonly object? _message;

            public Redacted(string redactionType, object? message)
            {
                _redactionType = redactionType;
                _message = message;
            }

            public override string ToString() => $"<{_redactionType}>{_message}</{_redactionType}>";
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
