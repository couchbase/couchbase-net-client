using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace Couchbase.Test.Common.Utils
{
    public class TestOutputLogger : ILogger
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly string _categoryName;

        public TestOutputLogger(ITestOutputHelper outputHelper, string categoryName)
        {
            _outputHelper = outputHelper;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => new Moq.Mock<IDisposable>().Object;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                _outputHelper.WriteLine($"{logLevel}: {_categoryName} [{eventId}] {formatter(state, exception)}");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    public class TestOutputLoggerFactory : ILoggerFactory
    {
        private readonly ITestOutputHelper _outputHelper;

        public TestOutputLoggerFactory(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new TestOutputLogger(_outputHelper, categoryName);

        public void Dispose()
        {
        }
    }


}
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
