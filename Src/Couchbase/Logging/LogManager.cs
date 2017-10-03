using System;
#if NETSTANDARD
using Microsoft.Extensions.Logging;
#endif

namespace Couchbase.Logging
{
    public static class LogManager
    {
        public static ILog GetLogger<T>()
        {
            return GetLogger(typeof(T));
        }

#if NET45
        public static ILog GetLogger(Type type)
        {
            return new CommonLoggingLogger(Common.Logging.LogManager.GetLogger(type));
        }
#endif

#if NETSTANDARD
        private static ILoggerFactory _factory;

        public static void ConfigureLoggerFactory(ILoggerFactory factory)
        {
            _factory = factory;
        }

        public static ILog GetLogger(Type type)
        {
            if (_factory == null)
            {
                _factory = new LoggerFactory();
            }
            return new MicrosoftLoggingLogger(_factory.CreateLogger(type));
        }
#endif
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
