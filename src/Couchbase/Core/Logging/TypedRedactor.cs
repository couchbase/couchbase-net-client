using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Represents a logging argument that is redactable. This provides a more strongly-typed version of redaction
    /// than exposed by the public <see cref="IRedactor"/> interface.
    /// </summary>
    /// <remarks>
    /// This type doesn't have an interface and is injected by the class so that methods may be inlined.
    /// </remarks>
    internal sealed class TypedRedactor
    {
        private const string _user = "ud";
        private const string _meta = "md";
        private const string _system = "sd";

        public TypedRedactor(ClusterOptions options) : this(options.RedactionLevel)
        {
        }

        internal TypedRedactor(RedactionLevel redactionLevel)
        {
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

        // The overloads below are for error contexts, which store plain strings rather than
        // Redacted<T>. They pass null and empty through untouched: Redacted<T>.ToString() renders
        // a null value as an empty string, which would turn an absent context field into a
        // present, empty one, and tagging an empty value yields a useless "<ud></ud>".
        // When RedactionLevel is None these return the same string instance, so the default
        // configuration stays allocation-free.

        public string? UserDataString(string? value) =>
            string.IsNullOrEmpty(value) ? value : UserData(value).ToString();

        public string? MetaDataString(string? value) =>
            string.IsNullOrEmpty(value) ? value : MetaData(value).ToString();

        public string? SystemDataString(string? value) =>
            string.IsNullOrEmpty(value) ? value : SystemData(value).ToString();

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
            throw new ArgumentOutOfRangeException(Enum.GetName(typeof(RedactionLevel), redactionLevel),
                "Unexpected redaction level: {redactionLevel}");
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
