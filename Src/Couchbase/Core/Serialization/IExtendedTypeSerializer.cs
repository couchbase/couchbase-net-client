using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// Provides an interface for serialization and deserialization of K/V pairs, with support for more
    /// advanced deserialization features.
    /// </summary>
    public interface IExtendedTypeSerializer : ITypeSerializer
    {
        /// <summary>
        /// Informs consumers what deserialization options this <see cref="IExtendedTypeSerializer"/> supports.
        /// </summary>
        SupportedDeserializationOptions SupportedDeserializationOptions { get; }

        /// <summary>
        /// Provides custom deserialization options.  Options not listed in <see cref="IExtendedTypeSerializer.SupportedDeserializationOptions"/>
        /// will be ignored.  If null, then defaults will be used.
        /// </summary>
        DeserializationOptions DeserializationOptions { get; set; }

        /// <summary>
        /// Get the name which will be used for a given member during serialization/deserialization.
        /// </summary>
        /// <param name="member">Returns the name of this member.</param>
        /// <returns>
        /// The name which will be used for a given member during serialization/deserialization,
        /// or null if if will not be serialized.
        /// </returns>
        string GetMemberName(MemberInfo member);
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
