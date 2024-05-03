using System;
using System.Runtime.InteropServices;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Wraps a value in an optional pair of redaction tags when written to a string. String formatting is
    /// delayed the call to ToString. This avoids the string formatting cost for disabled log levels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Since this type is a structure, it avoids heap allocations so long as we're using strongly typed
    /// logging mechanisms to avoid boxing. Making it generic also allows .NET 6 and C# 10 to use more
    /// efficient string building paradigms such as ISpanFormattable.
    /// </para>
    /// <para>
    /// Because this type implements ISpanFormattable in .NET 6 it also avoids string allocations when used
    /// in string interpolation expressions so long as <typeparamref name="T"/> also implements ISpanFormattable.
    /// There are some cases where generated C# logging methods use string interpolation to build the log message.
    /// </para>
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct Redacted<T>
#if NET6_0_OR_GREATER
        : ISpanFormattable
#else
        : IFormattable
#endif
    {
        private readonly T _value;
        private readonly string? _redactionType;

        /// <summary>
        /// Creates a no-op redaction, the value is not marked for redaction.
        /// </summary>
        /// <param name="value">Value to wrap.</param>
        public Redacted(T value) : this(value, null)
        {
        }

        /// <summary>
        /// Creates a redaction of the given type.
        /// </summary>
        /// <param name="value">Value to wrap.</param>
        /// <param name="redactionType">The type of redaction, or null to not redact.</param>
        public Redacted(T value, string? redactionType)
        {
            _value = value;
            _redactionType = redactionType;
        }

        public override string ToString() => ToString(null, null);

        // Note: For consistency with the various formatting methods available, we ignore the format parameter.
        // It isn't necessary for logging anyway.
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            if (_redactionType is null)
            {
                // Redaction is disabled, just format the value

                return _value is IFormattable formattable
                    ? formattable.ToString(null, formatProvider) // For consistency, don't forward the format
                    : _value?.ToString() ?? "";
            }

#if NET6_0_OR_GREATER
            // Use a stack allocated scratch buffer, this should be plenty for the vast majority of logged fields.
            // Additional space will be rented from the ArrayPool by string.Create if we exceed the buffer.
            Span<char> buffer = stackalloc char[128];
            return string.Create(formatProvider, buffer, $"<{_redactionType}>{_value}</{_redactionType}>");
#else
            // Any pre .NET 6 string building solution is going to make a string for _value before concatenating
            // so there is no added cost to doing it ourselves and passing a format provider.
            var formattedMessage = _value is IFormattable formattable2
                ? formattable2.ToString(null, formatProvider) // For consistency, don't forward the format
                : _value?.ToString() ?? "";

            return $"<{_redactionType}>{formattedMessage}</{_redactionType}>";
#endif
        }

#if NET6_0_OR_GREATER
        // Implementing ISpanFormattable.TryFormat allows string interpolation to write the value directly to the
        // destination without allocating an intermediate string.
        /// <inheritdoc />
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            // Span<char>.TryWrite uses a stack allocated string interpolation handler, so it's formatting to the span without heap allocation
            return _redactionType != null
                ? destination.TryWrite(provider, $"<{_redactionType}>{_value}</{_redactionType}>", out charsWritten)
                : destination.TryWrite(provider, $"{_value}", out charsWritten);
        }
#endif
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
