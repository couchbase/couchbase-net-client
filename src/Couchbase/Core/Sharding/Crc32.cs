using System;
using System.Security.Cryptography;

namespace Couchbase.Core.Sharding
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
