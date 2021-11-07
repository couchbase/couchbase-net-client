using System;
using System.Collections.Generic;

namespace Couchbase.Management.Analytics.Link
{
    public abstract record AnalyticsLink(string Name, string Dataverse)
    {
        public abstract string LinkType { get; }

        internal IEnumerable<KeyValuePair<string, string>> FormData => GetFormData();

        protected virtual IEnumerable<KeyValuePair<string, string>> GetFormData()
        {
            yield return new KeyValuePair<string, string>("type", LinkType);
        }

        public virtual bool TryValidateForRequest(out List<string> errors)
        {
            errors = new();
            RequiredToBeSet(nameof(LinkType), LinkType, errors);
            RequiredToBeSet(nameof(Name), Name, errors);
            RequiredToBeSet(nameof(Dataverse), Dataverse, errors);
            return errors.Count == 0;
        }

        public void ValidateForRequest()
        {
            if (!TryValidateForRequest(out var errors))
            {
                throw new ArgumentException(string.Join(Environment.NewLine, errors));
            }
        }

        protected void RequiredToBeSet(string name, string value, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"'{name}' must be set.");
            }
        }

        // Note that EscapeDataString will escape "/", so we do the replace after escape instead of before
        internal string ManagementPath => $"analytics/link/{Uri.EscapeDataString(Dataverse).Replace(".", "/")}/{Uri.EscapeDataString(Name)}";
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
