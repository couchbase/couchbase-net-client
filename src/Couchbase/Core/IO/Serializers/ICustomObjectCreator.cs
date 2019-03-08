using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Used to control type creation during deserialization.  For example, it can be used to create object proxies.
    /// </summary>
    public interface ICustomObjectCreator
    {
        /// <summary>
        /// Determine if this creator can create a particular type.
        /// </summary>
        /// <param name="type">Type to test.</param>
        /// <returns>True if this creator can create a particular type.</returns>
        /// <remarks>Results of this method should be consistent for every call so that they can be cached.</remarks>
        bool CanCreateObject(Type type);

        /// <summary>
        /// Create an instance of a particular type with default values, ready to be populated by the deserializer.
        /// </summary>
        /// <param name="type">Type to create.</param>
        /// <returns>New instance of the type with default values, ready to be populated by the deserializer.</returns>
        object CreateObject(Type type);
    }
}
