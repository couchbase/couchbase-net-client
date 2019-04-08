using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
{
    internal abstract class SubDocSingularLookupBase<T> : SubDocSingularBase<T>
    {
        public override void WriteExtras(OperationBuilder builder)
        {
            Span<byte> buffer = stackalloc byte[CurrentSpec.DocFlags != SubdocDocFlags.None ? 4 : 3];

            Converter.FromInt16((short) Converter.GetStringByteCount(Path), buffer); //1-2
            Converter.FromByte((byte) CurrentSpec.PathFlags, buffer.Slice(2)); //3

            if (CurrentSpec.DocFlags != SubdocDocFlags.None)
            {
                Converter.FromByte((byte) CurrentSpec.DocFlags, buffer.Slice(3));
            }

            builder.Write(buffer);
        }

        public override byte[] CreateBody()
        {
            var pathLength = Converter.GetStringByteCount(Path);

            var buffer = new byte[pathLength];
            Converter.FromString(Path, buffer);
            return buffer;
        }

        public override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            // Do nothing, lookups don't return extras
        }

        public override bool CanRetry()
        {
            return ErrorCode == null || ErrorMapRequestsRetry();
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
