using System;
using System.Reflection;

#if NET452
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase.IO.Converters
{
    /// <summary>
    /// A factory for creating <see cref="IByteConverter"/> functories.
    /// </summary>
    public static class ConverterFactory
    {
        /// <summary>
        /// Gets a <see cref="Func{IByteConverter}"/> factory for the default converter: <see cref="DefaultConverter"/>
        /// </summary>
        /// <returns>A func for creating <see cref="DefaultConverter"/> instances.</returns>
        public static Func<IByteConverter> GetConverter()
        {
            return () => new DefaultConverter();
        }

#if NET452

        /// <summary>
        /// Gets a <see cref="Func{IByteConverter}"/> factory for custom <see cref="IByteConverter"/>s conifgured in the App.Config.
        /// </summary>
        /// <param name="element">The <see cref="ConverterElement"/> from the App.Config.</param>
        /// <returns>A func for creating custom <see cref="IByteConverter"/> instances.</returns>
        public static Func<IByteConverter> GetConverter(ConverterElement element)
        {
            return GetConverter(element.Type);
        }

#endif

        /// <summary>
        /// Gets a <see cref="Func{IByteConverter}"/> factory for custom <see cref="IByteConverter"/>s conifgured in the App.Config.
        /// </summary>
        /// <param name="typeName">The name of the type implementing <see cref="IByteConverter"/>.</param>
        /// <returns>A func for creating custom <see cref="IByteConverter"/> instances.</returns>
        public static Func<IByteConverter> GetConverter(string typeName)
        {
            var type = Type.GetType(typeName, true);
            return () => (IByteConverter)Activator.CreateInstance(type);
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
