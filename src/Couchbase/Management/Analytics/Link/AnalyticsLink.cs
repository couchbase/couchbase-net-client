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

        internal string ManagementPath => $"analytics/link/{Uri.EscapeUriString(Dataverse.Replace(".", "/"))}/{Uri.EscapeUriString(Name)}";
    }
}
