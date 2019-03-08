namespace Couchbase.Core.IO
{
    /// <see cref="http://code.google.com/p/memcached/wiki/BinaryProtocolRevamped#Packet_Structure"/>
    public struct HeaderOffsets
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
