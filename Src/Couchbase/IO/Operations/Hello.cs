using System;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations
{
    internal class Hello : OperationBase<short[]>
    {
        public override OperationCode OperationCode
        {
            get { return OperationCode.Hello; }
        }

        public Hello(string key, short[] value, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, value, null, transcoder, opaque, timeout)
        {
        }

        public override byte[] CreateBody()
        {
            var body = new byte[_value.Length * 2];
            for (var i = 0; i < _value.Length; i++)
            {
                var offset = i * 2;
                Converter.FromInt16(_value[i], body, offset);
            }

            return body;
        }

        public override byte[] CreateExtras()
        {
            return new byte[0];
        }

        public override short[] GetValue()
        {
            var result = default(short[]);
            if (Success && Data != null && Data.Length > 0)
            {
                try
                {
                    var buffer = Data.ToArray();
                    var offset = Header.BodyOffset;
                    result = new short[Header.BodyLength/2];

                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = Converter.ToInt16(buffer, offset + i*2);
                    }
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return result;
        }

        public override bool RequiresKey
        {
            get { return true; }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
