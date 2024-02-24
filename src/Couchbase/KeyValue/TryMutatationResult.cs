using System;
using Couchbase.Core;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;

namespace Couchbase.KeyValue;

/// <summary>
/// Provides an interface for 'mutating' a document, but instead of throwing
/// <see cref="DocumentNotFoundException"/> exception if the document key is
/// not found, allows for the existence to be checked via <see cref="ITryMutationResult.Exists"/>.
/// </summary>
internal class TryMutationResult : TryResultBase, ITryMutationResult
{
    private readonly IMutationResult _mutationResult;

    public TryMutationResult(IMutationResult mutationResult)
    {
        _mutationResult = mutationResult;
        Status = ((IResponseStatus)mutationResult).Status;
    }
    public ulong Cas => _mutationResult.Cas;

    public MutationToken MutationToken
    {
        get => _mutationResult.MutationToken;
        set => throw new InvalidOperationException("Not settable via TryMutationState.");
    }
}
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2023 Couchbase, Inc.
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

