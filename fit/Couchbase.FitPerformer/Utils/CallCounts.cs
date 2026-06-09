using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Grpc.Protocol.Hooks.Transactions;

namespace Couchbase.FitPerformer.Utils
{
    public class CallCounts
    {
        private readonly ConcurrentDictionary<HookPoint, int> _countsPerHook =
            new ConcurrentDictionary<HookPoint, int>();

        private readonly ConcurrentDictionary<Tuple<HookPoint, string>, int> _countsPerHookAndParam =
            new ConcurrentDictionary<Tuple<HookPoint, string>, int>();

        public void Add(HookPoint hookPoint)
        {
            _countsPerHook.AddOrUpdate(hookPoint, 1, (key, oldValue) => oldValue + 1);
        }

        public void Add(HookPoint hookPoint, string param)
        {
            _countsPerHookAndParam.AddOrUpdate(Tuple.Create(hookPoint, param), 1, (key, oldValue) => oldValue + 1);
        }

        public int GetCount(HookPoint hookPoint)
        {
            try
            {
                return _countsPerHook[hookPoint];
            }
            catch (KeyNotFoundException)
            {
                return 0;
            }
        }

        public int GetCount(HookPoint hookPoint, string param)
        {
            try
            {
                return _countsPerHookAndParam[Tuple.Create(hookPoint, param)];
            }
            catch (KeyNotFoundException)
            {
                return 0;
            }
        }
    }
}