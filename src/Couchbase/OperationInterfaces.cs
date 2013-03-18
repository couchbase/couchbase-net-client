using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached;

namespace Couchbase
{
	public interface ITouchOperation : ISingleItemOperation { }
	public interface IGetAndTouchOperation : IGetOperation { }
	public interface IGetWithLockOperation : IGetOperation { }
	public interface IObserveOperation : IOperation {}
}
