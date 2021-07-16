using System;

namespace Couchbase.Core.Compatibility
{
    /// <summary>
    /// Annotation for the interface stability of a given API. A stable interface is one that
    /// is guaranteed not to change between versions, meaning that you may use an API of a
    /// given SDK version and be assured that the given API will retain the same parameters
    /// and behavior in subsequent versions. An unstable interface is one which may appear to
    /// work or behave in a specific way within a given SDK version, but may change in its
    /// behavior or arguments in future SDK versions, causing odd application behavior or
    /// compiler/API usage errors.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public class InterfaceStabilityAttribute : Attribute
    {
        public InterfaceStabilityAttribute(Level level)
        {
            Level = level;
        }

        /// <summary>
        /// The interface stability of the API.
        /// </summary>
        public Level Level { get; }
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
