using System;
using Couchbase.Logging;

namespace Couchbase.Core.Diagnostics
{
    public static class TimingFactory
    {
        private static ITimingStore _store;
        private static volatile object _lockObj = new object();
        private static ILog Log = LogManager.GetLogger<OperationTimer>();

        public static Func<TimingLevel, object, IOperationTimer> GetTimer()
        {
            if (_store != null) return (level, target) => new OperationTimer(level, target, _store);
            lock (_lockObj)
            {
                if (_store == null)
                {
                    _store = new CommonLogStore(Log);
                }
            }
            return (level, target) => new OperationTimer(level, target, _store);
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
