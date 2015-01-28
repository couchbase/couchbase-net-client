using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;

namespace Couchbase.Exceptions
{
    public class DeadNodeException : ViewException
    {
        public DeadNodeException(string designDoc, string viewName, string error, string reason):
            base(designDoc, viewName, error, reason) {}

        public DeadNodeException(string message) : base(message)
        {
        }
    }
}