using System;
using System.Configuration;
using System.Reflection;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;

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
            return () => new DefaultTranscoder(config.Converter(),
                config.DeserializationSettings,
                config.SerializationSettings);
        }

        /// <summary>
        /// Gets a Func for creating <see cref="ITypeTranscoder"/> transcoders.
        /// </summary>
        /// <param name="config">The current <see cref="ClientConfiguration"/>.</param>
        /// <param name="element">The <see cref="TranscoderElement"/> from the App.Config or Web.Config.</param>
        /// <returns>A <see cref="Func{ITypeTranscoder}"/> for creating <see cref="ITypeTranscoder"/>s.</returns>
        public static Func<ITypeTranscoder> GetTranscoder(ClientConfiguration config, TranscoderElement element)
        {
            var parameters = new object[]
            {
                config.Converter(),
                config.DeserializationSettings,
                config.SerializationSettings
            };

            var type = Type.GetType(element.Type, true);
            return () => (ITypeTranscoder) Activator.CreateInstance(type, parameters);
        }
    }
}
