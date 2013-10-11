using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Exceptions
{
	/// <summary>
	/// Standard exception class thrown on when view errors are encountered
	/// </summary>
	public class ViewException : Exception
	{
		public string Error { get; set; }

		public string Reason { get; set; }

		public ViewException(string designDoc, string viewName, string error, string reason) :
			base(string.Format("Query failed for view {0} in design document {1}", viewName, designDoc))
		{
			Error = error;
			Reason = reason;
		}

	    public ViewException(string message, Exception innerException)
            : base(message, innerException)
	    {
	    }

        public ViewException(string message)
            : base(message)
	    {
	    }
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2013 Couchbase, Inc.
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
#endregion