using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// Options to control deserialization process in an <see cref="IExtendedTypeSerializer"/>.
    /// </summary>
    public class DeserializationOptions
    {
        /// <summary>
        /// Returns true if any custom options are set
        /// </summary>
        public bool HasSettings
        {
            get
            {
                return CustomObjectCreator != null;
            }
        }

        /// <summary>
        /// <see cref="ICustomObjectCreator"/> to use when creating objects during deserialization.
        /// Null will uses the <see cref="IExtendedTypeSerializer"/> defaults for type creation.
        /// </summary>
        public ICustomObjectCreator CustomObjectCreator { get; set; }
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
