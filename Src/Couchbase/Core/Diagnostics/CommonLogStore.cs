using Couchbase.Logging;

namespace Couchbase.Core.Diagnostics
{
    public class CommonLogStore : ITimingStore
    {
        private readonly ILog _log;
        public CommonLogStore(ILog log)
        {
            _log = log;
        }

        public void Write(string format, params object[] args)
        {
            _log.Info(format, args);
        }

        public bool Enabled
        {
            get { return _log != null && _log.IsDebugEnabled; }
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
