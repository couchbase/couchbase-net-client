using System;

namespace Couchbase.Core.Sharding
{
    internal static class Crc32
    {
        private const uint Polynomial = 0xedb88320u;
        private static readonly uint[] Table = new uint[16  * 256];

        static Crc32()
        {
            var table = Table;
            for (uint i = 0; i < 256; i++)
            {
                uint res = i;
                for (int t = 0; t < 16; t++)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        res = (res & 1) == 1
                            ? Polynomial ^ (res >> 1)
                            : (res >> 1);
                    }

                    table[(t * 256) + i] = res;
                }
            }
        }

        public static uint ComputeHash(ReadOnlySpan<byte> bytes)
        {
            var hash = uint.MaxValue;
            var table = Table;

            // Process in 16 byte chunks (the CPU can do some stuff in parallel)
            while (bytes.Length >= 16)
            {
                var a = table[(3 * 256) + bytes[12]]
                        ^ table[(2 * 256) + bytes[13]]
                        ^ table[(1 * 256) + bytes[14]]
                        ^ table[(0 * 256) + bytes[15]];

                var b = table[(7 * 256) + bytes[8]]
                        ^ table[(6 * 256) + bytes[9]]
                        ^ table[(5 * 256) + bytes[10]]
                        ^ table[(4 * 256) + bytes[11]];

                var c = table[(11 * 256) + bytes[4]]
                        ^ table[(10 * 256) + bytes[5]]
                        ^ table[(9 * 256) + bytes[6]]
                        ^ table[(8 * 256) + bytes[7]];

                var d = table[(15 * 256) + ((byte)hash ^ bytes[0])]
                        ^ table[(14 * 256) + ((byte)(hash >> 8) ^ bytes[1])]
                        ^ table[(13 * 256) + ((byte)(hash >> 16) ^ bytes[2])]
                        ^ table[(12 * 256) + ((hash >> 24) ^ bytes[3])];

                hash = d ^ c ^ b ^ a;
                bytes = bytes.Slice(16);
            }

            // Process the remaining bytes, if any
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < bytes.Length; i++)
            {
                hash = table[(byte)(hash ^ bytes[i])] ^ hash >> 8;
            }

            return (~hash >> 16) & 0x7fff;
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
