using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Used to control type creation during deserialization.  For example, it can be used to create object proxies.
    /// </summary>
    public interface ICustomObjectCreator
    {
        /// <summary>
        /// Determine if this creator can create a particular type.
        /// </summary>
        /// <param name="type">Type to test.</param>
        /// <returns>True if this creator can create a particular type.</returns>
        /// <remarks>Results of this method should be consistent for every call so that they can be cached.</remarks>
        bool CanCreateObject(Type type);

        /// <summary>
        /// Create an instance of a particular type with default values, ready to be populated by the deserializer.
        /// </summary>
        /// <param name="type">Type to create.</param>
        /// <returns>New instance of the type with default values, ready to be populated by the deserializer.</returns>
        object CreateObject(Type type);
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
