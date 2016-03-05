using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// Provides access to an <see cref="ITypeSerializer"/> related to the object.
    /// </summary>
    public interface ITypeSerializerProvider
    {
        /// <summary>
        /// Gets the <see cref="ITypeSerializer"/> related to the object.
        /// </summary>
        ITypeSerializer Serializer { get; }
    }
}
