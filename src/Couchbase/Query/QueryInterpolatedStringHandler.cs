#if NET6_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;

#nullable enable

namespace Couchbase.Query
{
    /// <summary>
    /// Provides a handler used by the language compiler to process interpolated strings into
    /// N1QL queries with positional parameters.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct QueryInterpolatedStringHandler
    {
        // Quick set of positional parameter names we can reuse
        private static readonly string[] Parameters =
        {
            "", "$1", "$2", "$3", "$4", "$5", "$6", "$7", "$8", "$9"
        };

        // This inner handler builds the N1QL query
        private DefaultInterpolatedStringHandler _innerHandler;

        // Options for the query, positional parameters are added to this object
        private readonly QueryOptions _queryOptions;

        // Tracks how many parameters have been added
        private int _parameterCount;

        /// <summary>
        /// The <see cref="QueryOptions"/> to be used when executing the query.
        /// </summary>
        /// <remarks>
        /// Positional parameters will be added to these options as <see cref="AppendFormatted(string?)"/>
        /// or its overloads are called.
        /// </remarks>
        public readonly QueryOptions QueryOptions => _queryOptions;

        /// <summary>
        /// Creates a handler used by the language compiler to process interpolated strings into
        /// N1QL queries with positional parameters.
        /// </summary>
        /// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <remarks>
        /// This is intended to be called only by compiler-generated code. Arguments are not validated as they'd
        /// otherwise be for members intended to be used directly.
        /// <see cref="QueryOptions"/> will be defaulted to AdHoc of <c>false</c>.
        /// </remarks>
        public QueryInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            _innerHandler = new DefaultInterpolatedStringHandler(literalLength, formattedCount);

            // Default to a prepared query since we're using parameters
            _queryOptions = new QueryOptions().AdHoc(false);

            _parameterCount = 0;
        }

        /// <summary>
        /// Creates a handler used by the language compiler to process interpolated strings into
        /// N1QL queries with positional parameters.
        /// </summary>
        /// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="queryOptions">Options to enrich with parameters as the interpolated string is processed.</param>
        /// <remarks>
        /// This is intended to be called only by compiler-generated code. Arguments are not validated as they'd
        /// otherwise be for members intended to be used directly.
        /// </remarks>
        public QueryInterpolatedStringHandler(int literalLength, int formattedCount, QueryOptions queryOptions)
        {
            _innerHandler = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
            _queryOptions = queryOptions;
            _parameterCount = 0;
        }

        /// <summary>
        /// Writes the specified string to the handler.
        /// </summary>
        /// <param name="value">The string to write.</param>
        public void AppendLiteral(string value)
        {
            _innerHandler.AppendLiteral(value);
        }

        /// <summary>
        /// Writes the specified value to the handler as a positional parameter.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void AppendFormatted(ReadOnlySpan<char> value)
        {
            AppendParameterReference();

            _queryOptions.Parameter(value.ToString());
        }

        /// <summary>Writes the specified value to the handler as a positional parameter.</summary>
        /// <param name="value">The value to write.</param>
        /// <param name="alignment">Ignored.</param>
        /// <param name="format">Ignored.</param>
        /// <remarks>
        /// Provided for API compatibility, <paramref name="alignment"/> and <paramref name="format"/> are ignored.
        /// </remarks>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) =>
            AppendFormatted(value);

        /// <summary>
        /// Writes the specified value to the handler as a positional parameter.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        public void AppendFormatted<T>(T value)
        {
            AppendParameterReference();

            _queryOptions.Parameter(value);
        }

        /// <summary>
        /// Writes the specified value to the handler as a positional parameter.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="format">Ignored.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        /// <remarks>
        /// Provided for API compatibility, <paramref name="format"/> is ignored.
        /// </remarks>
        public void AppendFormatted<T>(T value, string? format) =>
            AppendFormatted(value);

        /// <summary>
        /// Writes the specified value to the handler as a positional parameter.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="alignment">Ignored.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        /// <remarks>
        /// Provided for API compatibility, <paramref name="alignment"/> is ignored.
        /// </remarks>
        public void AppendFormatted<T>(T value, int alignment) =>
            AppendFormatted(value);

        /// <summary>
        /// Writes the specified value to the handler as a positional parameter.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="alignment">Ignored.</param>
        /// <param name="format">Ignored.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        /// <remarks>
        /// Provided for API compatibility, <paramref name="alignment"/> and <paramref name="format"/> are ignored.
        /// </remarks>
        public void AppendFormatted<T>(T value, int alignment, string? format) =>
            AppendFormatted(value);

        /// <summary>
        /// Writes the specified value to the handler as a positional parameter.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="alignment">Ignored.</param>
        /// <param name="format">Ignored.</param>
        /// <remarks>
        /// Provided for API compatibility, <paramref name="alignment"/> and <paramref name="format"/> are ignored.
        /// </remarks>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null) =>
            AppendFormatted(value);

        /// <summary>
        /// Writes the specified value to the handler as a positional parameter.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void AppendFormatted(string? value)
        {
            AppendParameterReference();

            _queryOptions.Parameter(value);
        }

        /// <summary>
        /// Writes the specified value to the handler as a positional parameter.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="alignment">Ignored.</param>
        /// <param name="format">Ignored.</param>
        /// <remarks>
        /// Provided for API compatibility, <paramref name="alignment"/> and <paramref name="format"/> are ignored.
        /// </remarks>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) =>
            AppendFormatted(value);

        /// <summary>
        /// Gets the built N1QL query.
        /// </summary>
        /// <returns>The built N1QL query.</returns>
        public readonly override string ToString() => _innerHandler.ToString();

        /// <summary>
        /// Gets the built N1QL query and clears the handler.
        /// </summary>
        /// <returns>The built N1QL query.</returns>
        /// <remarks>
        /// This releases any resources used by the handler. The method should be invoked only
        /// once and as the last thing performed on the handler. Subsequent use is erroneous, ill-defined,
        /// and may destabilize the process, as may using any other copies of the handler after ToStringAndClear
        /// is called on any one of them.
        /// </remarks>
        public string ToStringAndClear() => _innerHandler.ToStringAndClear();

        private void AppendParameterReference()
        {
            var parameterCount = ++_parameterCount;
            if (parameterCount < Parameters.Length)
            {
                _innerHandler.AppendLiteral(Parameters[parameterCount]);
            }
            else
            {
                // Fallback if we have more than 9 parameters
                _innerHandler.AppendLiteral("$");
                _innerHandler.AppendFormatted(++_parameterCount);
            }
        }
    }
}

#endif
