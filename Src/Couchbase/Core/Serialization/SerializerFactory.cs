using System;
using System.Collections.Generic;
using Couchbase.Configuration.Client;

#if NET45
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// A factory for creating <see cref="Func{ITypeSerializer}"/> factories.
    /// </summary>
    public static class SerializerFactory
    {
        /// <summary>
        /// Gets the <see cref="DefaultSerializer"/> serializer.
        /// </summary>
        /// <returns>A <see cref="Func{ITypeSerializer}"/> factory for creating <see cref="ITypeSerializer"/> objects.</returns>
        public static Func<ITypeSerializer> GetSerializer()
        {
            return () => new DefaultSerializer();
        }

#if NET45

        /// <summary>
        /// Gets the serializer.
        /// </summary>
        /// <param name="config">The current <see cref="ClientConfiguration"/> instance.</param>
        /// <param name="element">The <see cref="SerializerElement"/> that is defined in the App.Config.</param>
        /// <returns>A <see cref="Func{ITypeSerializer}"/> factory for creating <see cref="ITypeSerializer"/> objects.</returns>
        public static Func<ITypeSerializer> GetSerializer(ClientConfiguration config, SerializerElement element)
        {
            return GetSerializer(element.Type);
        }

#endif

        /// <summary>
        /// Gets the serializer.
        /// </summary>
        /// <param name="typeName">The name of the type implementing <see cref="ITypeSerializer"/>.</param>
        /// <returns>A <see cref="Func{ITypeSerializer}"/> factory for creating <see cref="ITypeSerializer"/> objects.</returns>
        public static Func<ITypeSerializer> GetSerializer(string typeName)
        {
            var type = Type.GetType(typeName, true);
            return () => (ITypeSerializer)Activator.CreateInstance(type);
        }
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
