#nullable enable
using System;
using System.Collections.Generic;
using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;

namespace Couchbase.Integrated.Transactions.Config
{
    /// <summary>
    /// A limited subset of <see cref="QueryOptions"/> that are usable in Transactions.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public record
        TransactionQueryOptions(
            QueryScanConsistency? ScanConsistency = null
        );
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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







