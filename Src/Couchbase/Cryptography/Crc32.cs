using System;
using System.Security.Cryptography;

namespace Couchbase.Cryptography
{
    internal sealed class Crc32 : HashAlgorithm
    {
        private const uint Polynomial = 0xedb88320u;
        private const uint Seed = 0xffffffffu;
        private static readonly uint[] Table = new uint[256];
        private uint _hash;

        static Crc32()
        {
            for (var i = 0u; i < Table.Length; ++i)
            {
                var temp = i;
                for (var j = 8u; j > 0; --j)
                {
                    if ((temp & 1) == 1)
                    {
                        temp = ((temp >> 1) ^ Polynomial);
                    }
                    else
                    {
                        temp >>= 1;
                    }
                }
                Table[i] = temp;
            }
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            _hash = Seed;
            for (var i = ibStart; i < cbSize - ibStart; i++)
            {
                _hash = (_hash >> 8) ^ Table[array[i] ^ _hash & 0xff];
            }
        }

        protected override byte[] HashFinal()
        {
            _hash = ((~_hash) >> 16) & 0x7fff;
            return BitConverter.GetBytes(_hash);
        }

        public override void Initialize()
        {
            _hash = Seed;
        }
    }
}

#region [ License information          ]

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

#endregion