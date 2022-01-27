using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Error.Internal;

namespace Couchbase.Transactions.Tests.IntegrationTests.Errors
{
    /// <summary>
    /// An exception class that should not raise out of Transactions.Run(), once thrown.
    /// </summary>
    internal class InternalIntegrationTestException : Exception, IClassifiedTransactionError
    {
        public ErrorClass CausingErrorClass { get; set; } = ErrorClass.FailOther;
    }

    public static class ErrorClassExtensions
    {
        public static Exception Throwable(this ErrorClass ec) =>
            new InternalIntegrationTestException() {CausingErrorClass = ec};
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