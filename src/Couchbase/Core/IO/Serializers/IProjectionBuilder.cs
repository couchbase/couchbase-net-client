using System;
using System.Collections.Generic;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Interface for projecting sub-document operations onto a target class.
    /// </summary>
    public interface IProjectionBuilder
    {
        /// <summary>
        /// Adds a value for a sub-document operation at a specific path.
        /// </summary>
        /// <param name="path">Path of the sub-document operation.</param>
        /// <param name="specValue">Data returned for the operation.</param>
        void AddPath(string path, ReadOnlyMemory<byte> specValue);

        /// <summary>
        /// Adds all children for a subset when an entire document is retreived in the spec.
        /// </summary>
        /// <param name="children">List of child attributes to be retained.</param>
        /// <param name="specValue">Data returned that represents the entire document.</param>
        /// <remarks>
        /// This is typically used when a large number of projections are requested as an optimization.
        /// </remarks>
        void AddChildren(IReadOnlyCollection<string> children, ReadOnlyMemory<byte> specValue);

        /// <summary>
        /// Converts the collected projections to a target object.
        /// </summary>
        /// <typeparam name="T">Type of object.</typeparam>
        /// <returns>The new object.</returns>
        T ToObject<T>();

        /// <summary>
        /// Converts one of the projections to a target primitive type.
        /// </summary>
        /// <typeparam name="T">Primitive type.</typeparam>
        /// <returns>The value of the projections.</returns>
        T ToPrimitive<T>();
    }
}
