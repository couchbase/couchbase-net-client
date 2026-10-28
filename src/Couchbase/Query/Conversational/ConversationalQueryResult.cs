using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace Couchbase.Query.Conversational
{
    internal sealed class ConversationalQueryResult<T> : IConversationalQueryResult<T>
    {
        private readonly IQueryResult<T> _result;
        private readonly NaturalLanguageResponse? _naturalLanguage;

        public ConversationalQueryResult(IQueryResult<T> result)
        {
            _result = result;
            _naturalLanguage = (result as QueryResultBase<T>)?.NaturalLanguage;
            MetaData = new ConversationalQueryMetaData(result.MetaData, _naturalLanguage);
        }

        internal string? ChatId => _naturalLanguage?.ChatId;

        public IAsyncEnumerable<T> Rows => _result.Rows;

        public string? GeneratedStatement => _naturalLanguage?.GeneratedStatement;

        public ConversationalQueryMetaData MetaData { get; }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            _result.GetAsyncEnumerator(cancellationToken);

        public void Dispose() => _result.Dispose();
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
