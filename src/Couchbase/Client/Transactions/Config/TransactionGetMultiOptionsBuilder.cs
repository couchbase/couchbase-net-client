#nullable enable

using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.KeyValue;

namespace Couchbase.Client.Transactions.Config;

public abstract class TransactionGetMultiOptionsBuilderBase
{
    protected IRequestSpan? _span;
    protected TransactionGetMultiMode _mode = TransactionGetMultiMode.PrioritizeLatency;


    public TransactionGetMultiOptionsBuilderBase Span(IRequestSpan span)
    {
        _span = span;
        return this;
    }

    public TransactionGetMultiOptionsBuilderBase Mode(
        TransactionGetMultiMode mode)
    {
        _mode = mode;
        return this;
    }
}

public class TransactionGetMultiOptionsBuilder :  TransactionGetMultiOptionsBuilderBase
{
    public static TransactionGetMultiOptionsBuilder Create()
    {
        return new TransactionGetMultiOptionsBuilder();
    }

    public static TransactionGetMultiOptionsBuilder Default => Create();

    public TransactionGetMultiOptions Build()
    {
        return new TransactionGetMultiOptions(_mode, _span);
    }
}

public class TransactionGetMultiReplicaFromPreferredServerGroupOptionsBuilder :
    TransactionGetMultiOptionsBuilderBase
{
    public static TransactionGetMultiReplicaFromPreferredServerGroupOptionsBuilder Create()
    {
        return new TransactionGetMultiReplicaFromPreferredServerGroupOptionsBuilder();
    }

    public TransactionGetMultiReplicaFromPreferredServerGroupOptions Build()
    {
        return new TransactionGetMultiReplicaFromPreferredServerGroupOptions(_mode, _span);
    }

    public static TransactionGetMultiReplicaFromPreferredServerGroupOptionsBuilder Default =>
        Create();
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2025 Couchbase, Inc.
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
