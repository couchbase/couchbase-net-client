using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Wraps logging arguments in redaction tags according to the configured <see cref="Logging.RedactionLevel"/>.
    /// </summary>
    /// <remarks>
    /// Injected as this concrete class rather than as an interface: the generic methods return
    /// <see cref="Redacted{T}"/> without boxing, and can be inlined.
    /// </remarks>
    internal sealed class Redactor : IRedactor
    {
        private const string _user = "ud";
        private const string _meta = "md";
        private const string _system = "sd";

        public Redactor(ClusterOptions options) : this(options.RedactionLevel)
        {
        }

        internal Redactor(RedactionLevel redactionLevel)
        {
            // Rejected here rather than only on the redaction path, where it would surface as an
            // exception from an arbitrary log statement instead of from building the cluster.
            if (redactionLevel is not (RedactionLevel.None or RedactionLevel.Partial or RedactionLevel.Full))
            {
                ThrowArgumentOutOfRangeException(redactionLevel);
            }

            RedactionLevel = redactionLevel;
        }

        public RedactionLevel RedactionLevel { get; }

        public Redacted<T> UserData<T>(T message)
        {
            return RedactMessage(message, _user);
        }

        public Redacted<T> MetaData<T>(T message)
        {
            return RedactMessage(message, _meta);
        }

        public Redacted<T> SystemData<T>(T message)
        {
            return RedactMessage(message, _system);
        }

        // Implemented explicitly so that these boxing, object-typed overloads can never win overload resolution
        // against the generic methods above; only a caller holding an IRedactor reference can reach them.

        [return: NotNullIfNotNull("message")]
        object? IRedactor.UserData(object? message) => message is not null ? UserData(message) : null;

        [return: NotNullIfNotNull("message")]
        object? IRedactor.MetaData(object? message) => message is not null ? MetaData(message) : null;

        [return: NotNullIfNotNull("message")]
        object? IRedactor.SystemData(object? message) => message is not null ? SystemData(message) : null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Redacted<T> RedactMessage<T>(T message, string redactionType)
        {
            switch (RedactionLevel)
            {
                case RedactionLevel.None:
                    return new Redacted<T>(message);

                case RedactionLevel.Full:
                    break;

                case RedactionLevel.Partial:
                    if (!ReferenceEquals(redactionType, _user))
                    {
                        return new Redacted<T>(message);
                    }
                    break;

                default:
                    ThrowArgumentOutOfRangeException(RedactionLevel);
                    break;
            }

            return new Redacted<T>(message, redactionType);
        }

        // Allow RedactMessage to be inlined
        [DoesNotReturn]
        private static void ThrowArgumentOutOfRangeException(RedactionLevel redactionLevel)
        {
            // Names the option the caller set, not this private parameter, and reports the value:
            // Enum.GetName is null for exactly the undefined values this rejects.
            throw new ArgumentOutOfRangeException(nameof(ClusterOptions.RedactionLevel), redactionLevel,
                "Unexpected redaction level.");
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
