using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached;

namespace Couchbase
{
	public interface ICouchbaseServerPool : IServerPool
	{
		new ICouchbaseOperationFactory OperationFactory { get; }
	}
}
