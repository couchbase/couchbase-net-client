using System;
using System.Diagnostics;
using Couchbase.IO.Operations;

namespace Couchbase.Core.Diagnostics
{
    internal class OperationTimer : IOperationTimer
    {
        private Stopwatch _stopwatch;
        private readonly TimingLevel _timingLevel;
        private const string DefaultFormat = "Timing: [{0} | {1} | {2} | {3,7:N3} | {4}]";
        private const string DefaultFormatNoLevel = "Timing: [{0} | {1} | {2,7:N2} | {3}]";
        private readonly Type _type;
        private readonly ulong _opaque;
        private readonly string _key;

        public OperationTimer(TimingLevel timingLevel, object target, ITimingStore store)
        {
            Store = store;
            if (!Store.Enabled) return;

            var op = target as IOperation;
            if (op == null) return;
            _timingLevel = timingLevel;
            _type = op.GetType();
            _opaque = op.Opaque;
            _key = op.Key;
            _stopwatch = Stopwatch.StartNew();
        }

        public ITimingStore Store { get; set; }

        public void Dispose()
        {
            if (!Store.Enabled) return;
            switch (_timingLevel)
            {
                case TimingLevel.None:
                    Store.Write(DefaultFormatNoLevel, _type.Name, _opaque,
                        _stopwatch.Elapsed.TotalMilliseconds, _key);
                    break;
                case TimingLevel.One:
                case TimingLevel.Two:
                case TimingLevel.Three:
                    Store.Write(DefaultFormat, _timingLevel, _type.Name, _opaque,
                        _stopwatch.Elapsed.TotalMilliseconds, _key);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _stopwatch = null;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
