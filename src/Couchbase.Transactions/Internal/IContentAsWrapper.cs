using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.KeyValue;
using Newtonsoft.Json.Linq;

namespace Couchbase.Transactions.Internal
{
    /// <summary>
    /// An interface for deferring ContentAs calls to their original source to avoid byte[]/json/string conversion in the middle.
    /// </summary>
    internal interface IContentAsWrapper
    {
        T ContentAs<T>();
    }

    internal class JObjectContentWrapper : IContentAsWrapper
    {
        private readonly object _originalContent;

        public JObjectContentWrapper(object originalContent)
        {
            _originalContent = originalContent;
        }

        public T ContentAs<T>() =>
            _originalContent is T asTyped ? asTyped : JObject.FromObject(_originalContent).ToObject<T>();
    }

    internal class LookupInContentAsWrapper : IContentAsWrapper
    {
        private readonly ILookupInResult _lookupInResult;
        private readonly int _specIndex;

        public LookupInContentAsWrapper(ILookupInResult lookupInResult, int specIndex)
        {
            _lookupInResult = lookupInResult;
            _specIndex = specIndex;
        }

        public T ContentAs<T>() => _lookupInResult.ContentAs<T>(_specIndex);
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
