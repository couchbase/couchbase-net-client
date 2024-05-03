namespace Couchbase.Core.IO
{
    /// <remarks>
    /// See http://code.google.com/p/memcached/wiki/BinaryProtocolRevamped#Packet_Structure
    /// </remarks>
    internal static class HeaderOffsets
    {
        public const byte MagicValue = 0x81;

        //byte position of item in header
        public const int Magic = 0;
        public const int Opcode = 1;
        public const int KeyLength = 2; // 2-3
        public const int ExtrasLength = 4;
        public const int Datatype = 5;
        public const int Status = 6; // 6-7
        public const int VBucket = 6; // 6-7
        public const int Body = 8; // 8-11
        public const int BodyLength = 8; // 8-11
        public const int Opaque = 12; // 12-15
        public const int Cas = 16; // 16-23

        public const int FramingExtras = 2;
        public const int AltKeyLength = 3;
        public const int HeaderLength = 24;
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
