#if NET45
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Couchbase.Core.Transcoders
{
    /// <summary>
    /// A binary level transcoder that can serialize / deserialize binary arrays using <see cref="BinaryFormatter"/>.
    /// </summary>
    /// <remarks>Any custom classes must have the <see cref="SerializableAttribute"/>.</remarks>
    public class BinaryTranscoder : DefaultTranscoder
    {
        private readonly BinaryFormatter _formatter = new BinaryFormatter();

        public override T DeserializeAsJson<T>(byte[] buffer, int offset, int length)
        {
            if (length == 0 || buffer.Length < offset + length)
            {
                return default(T);
            }

            using (var stream = new MemoryStream(buffer, offset, length))
            {
                return (T) _formatter.Deserialize(stream);
            }
        }

        public override byte[] SerializeAsJson(object value)
        {
            if (value == null)
            {
                return new byte[0];
            }

            using (var ms = new MemoryStream())
            {
                _formatter.Serialize(ms, value);
                return ms.ToArray();
            }
        }
    }
}
#endif

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
