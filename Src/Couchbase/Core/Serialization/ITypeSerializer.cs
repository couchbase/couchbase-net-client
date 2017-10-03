using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// Provides an interface for serialization and deserialization of K/V pairs.
    /// </summary>
    public interface ITypeSerializer
    {
        /// <summary>
        /// Deserializes the specified buffer into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="buffer">The buffer to deserialize from.</param>
        /// <param name="offset">The offset of the buffer to start reading from.</param>
        /// <param name="length">The length of the buffer to read from.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        T Deserialize<T>(byte[] buffer, int offset, int length);

        /// <summary>
        /// Deserializes the specified stream into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        T Deserialize<T>(Stream stream);

        /// <summary>
        /// Serializes the specified object into a buffer.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A <see cref="byte"/> array that is the serialized value of the key.</returns>
        byte[] Serialize(object obj);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
