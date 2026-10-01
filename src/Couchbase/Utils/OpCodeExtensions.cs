using System;
using Couchbase.Core.IO.Operations;
using static Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.ServiceSpan;

#nullable enable

namespace Couchbase.Utils
{
    internal static class OpCodeExtensions
    {
        internal static string? ToMetricTag(this OpCode opCode) =>
            opCode switch
            {
                OpCode.Get => Kv.Get,
                OpCode.GetL => Kv.GetAndLock,
                OpCode.GAT => Kv.GetAndTouch,
                OpCode.ReplicaRead => Kv.ReplicaRead,
                OpCode.Set => Kv.SetUpsert,
                OpCode.Add => Kv.AddInsert,
                OpCode.Replace => Kv.Replace,
                OpCode.Delete => Kv.DeleteRemove,
                OpCode.Append => Kv.Append,
                OpCode.Prepend => Kv.Prepend,
                OpCode.Increment => Kv.Increment,
                OpCode.Decrement => Kv.Decrement,
                OpCode.MultiLookup => Kv.LookupIn,
                OpCode.SubMultiMutation => Kv.MutateIn,
                OpCode.Touch => Kv.Touch,
                OpCode.Unlock => Kv.Unlock,
                OpCode.Observe => Kv.Observe,

                // The range scan opcodes are the one place a KV operation's metric tag does not
                // match its span name: the tag is the cross-SDK "rangescancreate" rather than the
                // span's "range_scan_create", so these cannot reuse a Kv.* constant. See NCBC-4314.
                OpCode.RangeScanCreate => "rangescancreate",
                OpCode.RangeScanContinue => "rangescancontinue",
                OpCode.RangeScanCancel => "rangescancancel",

                _ => opCode.ToString()
            };
    }
}
