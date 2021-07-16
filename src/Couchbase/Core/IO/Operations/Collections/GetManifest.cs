using System;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Core.IO.Operations.Collections
{
    internal class GetManifest :  OperationBase<Manifest>
    {
        public override OpCode OpCode  => OpCode.GetCollectionsManifest;

        protected override void BeginSend()
        {
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Flags.DataFormat,
                TypeCode = TypeCode.Object
            };
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
        }

        protected override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            //force it to treat the result as JSON for serialization
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Flags.DataFormat,
                TypeCode = TypeCode.Object
            };
        }
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
