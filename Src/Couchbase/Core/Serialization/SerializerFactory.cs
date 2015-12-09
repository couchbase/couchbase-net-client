using System;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// A factory for creating <see cref="Func{ITypeSerializer}"/> factories.
    /// </summary>
    public static class SerializerFactory
    {
        /// <summary>
        /// Gets the <see cref="DefaultSerializer"/> serializer.
        /// </summary>
        /// <returns>A <see cref="Func{ITypeSerializer}"/> factory for creating <see cref="ITypeSerializer"/> objects.</returns>
        public static Func<ITypeSerializer> GetSerializer()
        {
            return () => new DefaultSerializer();
        }

        /// <summary>
        /// Gets the serializer.
        /// </summary>
        /// <param name="config">The current <see cref="ClientConfiguration"/> instance.</param>
        /// <param name="element">The <see cref="SerializerElement"/> that is defined in the App.Config.</param>
        /// <returns>A <see cref="Func{ITypeSerializer}"/> factory for creating <see cref="ITypeSerializer"/> objects.</returns>
        public static Func<ITypeSerializer> GetSerializer(ClientConfiguration config, SerializerElement element)
        {
            var type = Type.GetType(element.Type, true);
            return () => (ITypeSerializer)Activator.CreateInstance(type);
        }
    }
}
