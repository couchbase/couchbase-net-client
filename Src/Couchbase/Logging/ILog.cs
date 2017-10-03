using System;

namespace Couchbase.Logging
{
    public interface ILog
    {
        bool IsDebugEnabled { get; }

        void Trace(string message, params object[] args);
        void Trace(Exception exception);
        void Trace(string message, Exception exception);

        void Debug(string format, params object[] args);
        void Debug(Exception exception);
        void Debug(string message, Exception exception);

        void Info(string format, params object[] args);
        void Info(Exception exception);
        void Info(string message, Exception exception);

        void Warn(string message, params object[] args);
        void Warn(Exception exception);
        void Warn(string message, Exception exception);

        void Error(string message, params object[] args);
        void Error(Exception exception);
        void Error(string message, Exception exception);
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
