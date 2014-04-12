using System;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace Couchbase.IO.Operations
{
    public struct OperationHeader
    {
        private const int HeaderLength = 24;

        public int Magic { get; set; }

        public OperationCode OperationCode { get; set; }

        public string Key { get; set; }

        public int ExtrasLength { get; set; }

        public TypeCode DataType { get; set; }

        public ResponseStatus Status { get; set; }

        public int KeyLength { get; set; }

        public int BodyLength { get; set; }

        public uint Opaque { get; set; }

        public ulong Cas { get; set; }

        public bool HasData()
        {
            return BodyLength > 0;
        }

        public int TotalLength { get { return BodyLength + HeaderLength; } }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("------HEADER------{0}", Environment.NewLine);
            sb.AppendFormat("Magic: {0}{1}", Magic, Environment.NewLine);
            sb.AppendFormat("OpCode: {0}{1}", OperationCode, Environment.NewLine);
            sb.AppendFormat("Key: {0}{1}", Key, Environment.NewLine);
            sb.AppendFormat("ExtrasLength: {0}{1}", ExtrasLength, Environment.NewLine);
            sb.AppendFormat("DataType: {0}{1}", DataType, Environment.NewLine);
            sb.AppendFormat("Status: {0}{1}", Status, Environment.NewLine);
            sb.AppendFormat("Opaque: {0}{1}", Opaque, Environment.NewLine);
            sb.AppendFormat("Cas: {0}{1}", Cas, Environment.NewLine);
            sb.Append("------HEADER (END)------");
            return sb.ToString();
        }
    }
}
