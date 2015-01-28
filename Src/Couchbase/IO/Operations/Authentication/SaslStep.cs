using System;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations.Authentication
{
    /// <summary>
    /// Peforms the next step in the in SASL authentication process when required by a AuthenticationContinue message from a <see cref="SaslStart"/> operation.
    /// </summary>
    internal class SaslStep : SaslStart
    {
        public SaslStep(string key, string value, IByteConverter converter)
            : base(key, value, converter)
        {
        }
        public override byte[] CreateExtras()
        {
            Format = DataFormat.String;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.String
            };
            return new byte[0];
        }

        public override void ReadExtras(byte[] buffer)
        {
            Format = DataFormat.String;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.String
            };
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SaslStep; }
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion [ License information ]