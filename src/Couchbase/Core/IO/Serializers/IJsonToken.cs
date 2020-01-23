using System;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Used to support dynamic object reading during streaming JSON deserialization.
    /// </summary>
    /// <seealso cref="IJsonStreamReader" />
    public interface IJsonToken
    {
        /// <summary>
        /// Gets the value of a particular attribute of this token.
        /// Returns null if the attribute is not found.
        /// </summary>
        /// <param name="key">Name of the attribute.</param>
        IJsonToken? this[string key] { get; }

        /// <summary>
        /// Deserializes the token to an object.
        /// </summary>
        /// <typeparam name="T">Type of object.</typeparam>
        /// <returns>The object.</returns>
        T ToObject<T>();

        /// <summary>
        /// Returns the token cast as a particular type, such as a string or integer.
        /// </summary>
        /// <typeparam name="T">Type to cast.</typeparam>
        /// <returns>The value.</returns>
        T Value<T>();

        /// <summary>
        /// Returns a dynamic object representing the current token.
        /// </summary>
        /// <returns>The dynamic object.</returns>
        dynamic ToDynamic();
    }
}
