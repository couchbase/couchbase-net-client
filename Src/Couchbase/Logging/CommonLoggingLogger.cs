#if NET452
using System;

namespace Couchbase.Logging
{
    internal class CommonLoggingLogger : ILog
    {
        private readonly Common.Logging.ILog _log;

        public bool IsDebugEnabled { get { return _log.IsDebugEnabled; } }

        public CommonLoggingLogger(Common.Logging.ILog log)
        {
            _log = log;
        }

        public void Trace(string message, params object[] args)
        {
            _log.TraceFormat(message, args);
        }

        public void Trace(Exception exception)
        {
            _log.Trace(exception);
        }

        public void Trace(string message, Exception exception)
        {
            _log.Trace(message, exception);
        }

        public void Debug(string format, params object[] args)
        {
            _log.DebugFormat(format, args);
        }

        public void Debug(Exception exception)
        {
            _log.Debug(exception);
        }

        public void Debug(string message, Exception exception)
        {
            _log.Debug(message, exception);
        }

        public void Info(string format, params object[] args)
        {
            _log.InfoFormat(format, args);
        }

        public void Info(Exception exception)
        {
            _log.Info(exception);
        }

        public void Info(string message, Exception exception)
        {
            _log.Info(message, exception);
        }

        public void Warn(string message, params object[] args)
        {
            _log.WarnFormat(message, args);
        }

        public void Warn(Exception exception)
        {
            _log.Warn(exception);
        }

        public void Warn(string message, Exception exception)
        {
            _log.Warn(message, exception);
        }

        public void Error(string message, params object[] args)
        {
            _log.ErrorFormat(message, args);
        }

        public void Error(Exception exception)
        {
            _log.Error(exception);
        }

        public void Error(string message, Exception exception)
        {
            _log.Error(message, exception);
        }
    }
}
#endif

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
