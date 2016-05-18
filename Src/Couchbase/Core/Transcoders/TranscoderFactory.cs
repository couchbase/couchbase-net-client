using System;
using Couchbase.Configuration.Client;

#if NET45
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase.Core.Transcoders
{
    /// <summary>
    /// A factory for creating <see cref="ITypeTranscoder"/> functories.
    /// </summary>
    public static class TranscoderFactory
    {
        /// <summary>
        /// Gets a Func for creating <see cref="ITypeTranscoder"/> transcoders.
        /// </summary>
        /// <param name="config">The current <see cref="ClientConfiguration"/>.</param>
        /// <returns>A <see cref="Func{ITypeTranscoder}"/> for creating <see cref="ITypeTranscoder"/>s.</returns>
        public static Func<ITypeTranscoder> GetTranscoder(ClientConfiguration config)
        {
            return () => new DefaultTranscoder(config.Converter(), config.Serializer());
        }

#if NET45

        /// <summary>
        /// Gets a Func for creating <see cref="ITypeTranscoder"/> transcoders.
        /// </summary>
        /// <param name="config">The current <see cref="ClientConfiguration"/>.</param>
        /// <param name="element">The <see cref="TranscoderElement"/> from the App.Config or Web.Config.</param>
        /// <returns>A <see cref="Func{ITypeTranscoder}"/> for creating <see cref="ITypeTranscoder"/>s.</returns>
        public static Func<ITypeTranscoder> GetTranscoder(ClientConfiguration config, TranscoderElement element)
        {
            return GetTranscoder(config, element.Type);
        }

#endif

        /// <summary>
        /// Gets a Func for creating <see cref="ITypeTranscoder"/> transcoders.
        /// </summary>
        /// <param name="config">The current <see cref="ClientConfiguration"/>.</param>
        /// <param name="typeName">The name of the type implementing <see cref="ITypeTranscoder"/>.</param>
        /// <returns>A <see cref="Func{ITypeTranscoder}"/> for creating <see cref="ITypeTranscoder"/>s.</returns>
        public static Func<ITypeTranscoder> GetTranscoder(ClientConfiguration config, string typeName)
        {
            var parameters = new object[]
            {
                config.Converter(),
                config.Serializer()
            };

            var type = Type.GetType(typeName, true);
            return () => (ITypeTranscoder)Activator.CreateInstance(type, parameters);
        }
    }
}
