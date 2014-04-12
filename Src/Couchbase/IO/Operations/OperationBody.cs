using System;
using System.Text;

namespace Couchbase.IO.Operations
{
    public struct OperationBody
    {
        public ArraySegment<byte> Data { get; set; }

        public ArraySegment<byte> Extras { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("------BODY-----{0}", Environment.NewLine);
            sb.AppendFormat("{0}{1}", Encoding.UTF8.GetString(Data.Array), Environment.NewLine);
            sb.Append("------BODY (END)------");
            return sb.ToString();
        }
    }
}
