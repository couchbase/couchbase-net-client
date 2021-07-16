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
