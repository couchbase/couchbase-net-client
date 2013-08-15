using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// Provides functionality for tracing messages based upon a regular expression match. To use in an application
    /// add to a listener in the App.Config. The regex (set of keys) or a string literal (a single key) can be added to the initializeData field
    /// to filter the trace by a match.
    /// </summary>
    public class KeyFilter : TraceFilter
    {
        private Regex _regex;
        private string _expression;

        /// <summary>
        /// Primary ctor to use - called internally by BCL infrastructure based off app.config settings
        /// </summary>
        /// <param name="initializeData">This should be a string literal key or a regex for filtering sets of keys</param>
        public KeyFilter(string initializeData) :
            this(initializeData, new Regex(initializeData))
        {
        }

        public KeyFilter(string initializeData, Regex regex)
        {
            _expression = initializeData;
            _regex = regex;
        }


        public override bool ShouldTrace(TraceEventCache cache, string source, TraceEventType eventType, int id, string formatOrMessage, object[] args, object data1, object[] data)
        {
            //Only support information event types atm. Currently ignoring the formatOrMessage 
            //field which could be supported as well if different EventTypes were supported -
            //Also there are some corner cases that are missing in the logic below.
            if (eventType == TraceEventType.Information)
            {
                string stringToMatch = null;
                if (data1 != null)
                {
                    stringToMatch = data1.ToString();
                }
                if (stringToMatch == null && data != null && data.Length > 0)
                {
                    stringToMatch = data.First().ToString();
                }
                if (stringToMatch == null && formatOrMessage != null)
                {
                    stringToMatch = formatOrMessage;
                }
                if (stringToMatch == null)
                {
                    throw new ArgumentNullException("data", "Tracing requires a non-null value.");
                }
                return _regex.IsMatch(stringToMatch);
            }
            throw new NotSupportedException("Only TraceEventTypes of Verbose and Information are currently supported.");
        }
    }
}
