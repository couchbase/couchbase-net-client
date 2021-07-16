
namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    ///     Supplied by <see cref="IExtendedTypeSerializer" /> to define which deserialization options it supports.
    /// </summary>
    /// <remarks>Intended to help support backwards compatibility as new deserialization options are added in the future.</remarks>
    public class SupportedDeserializationOptions
    {
        /// <summary>
        ///     If true, the <see cref="IExtendedTypeSerializer" /> supports
        ///     <see cref="DeserializationOptions.CustomObjectCreator" />.
        /// </summary>
        public bool CustomObjectCreator { get; set; }
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
