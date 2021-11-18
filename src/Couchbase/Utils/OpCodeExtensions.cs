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
                _ => opCode.ToString()
            };
    }
}
