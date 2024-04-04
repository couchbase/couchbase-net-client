using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionUrlBindingConverter : JsonConverter<EventingFunctionUrlBinding>
    {
        private static readonly JsonEncodedText AuthTypePropertyName = JsonEncodedText.Encode("auth_type");
        private static readonly JsonEncodedText HostnamePropertyName = JsonEncodedText.Encode("hostname");
        private static readonly JsonEncodedText AllowCookiesPropertyName = JsonEncodedText.Encode("allow_cookies");
        private static readonly JsonEncodedText ValidateSslCertificatePropertyName = JsonEncodedText.Encode("validate_ssl_certificate");
        private static readonly JsonEncodedText ValuePropertyName = JsonEncodedText.Encode("value");

        public static string? ReadAndGetDiscriminator(Utf8JsonReader reader, EventingFunctionUrlBinding result)
        {
            // We're using a non-ref copy of the reader, so anything we do in this method will not advance it

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                ThrowHelper.ThrowJsonException();
            }

            string? discriminator = null;

            // Read the property name
            if (!reader.Read())
            {
                ThrowHelper.ThrowJsonException();
            }
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    ThrowHelper.ThrowJsonException();
                }

                if (reader.ValueTextEquals(AuthTypePropertyName.EncodedUtf8Bytes))
                {
                    // Read the value
                    if (!reader.Read())
                    {
                        reader.Read();
                    }

                    discriminator = reader.GetString();
                }
                else if (reader.ValueTextEquals(HostnamePropertyName.EncodedUtf8Bytes))
                {
                    // Read the value
                    if (!reader.Read())
                    {
                        reader.Read();
                    }

                    result.Hostname = reader.GetString();
                }
                else if (reader.ValueTextEquals(AllowCookiesPropertyName.EncodedUtf8Bytes))
                {
                    // Read the value
                    if (!reader.Read())
                    {
                        reader.Read();
                    }

                    result.AllowCookies = reader.GetBoolean();
                }
                else if (reader.ValueTextEquals(ValidateSslCertificatePropertyName.EncodedUtf8Bytes))
                {
                    // Read the value
                    if (!reader.Read())
                    {
                        reader.Read();
                    }

                    result.ValidateSslCertificate = reader.GetBoolean();
                }
                else if (reader.ValueTextEquals(ValuePropertyName.EncodedUtf8Bytes))
                {
                    // Read the value
                    if (!reader.Read())
                    {
                        reader.Read();
                    }

                    result.Alias = reader.GetString();
                }
                else
                {
                    // Read the value
                    if (!reader.Read())
                    {
                        ThrowHelper.ThrowJsonException();
                    }

                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    {
                        // Skip the array or object to the next property name or end of the object
                        // This leaves us on the end of the object or array
                        if (!reader.TrySkip())
                        {
                            ThrowHelper.ThrowJsonException();
                        }
                    }
                }

                // Skip the value to the next property name or end of the object
                if (!reader.Read())
                {
                    ThrowHelper.ThrowJsonException();
                }
            }

            return discriminator;
        }

        public override EventingFunctionUrlBinding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new EventingFunctionUrlBinding();

            var discriminator = ReadAndGetDiscriminator(reader, result);
            var auth = discriminator switch
            {
                "no-auth" => JsonSerializer.Deserialize(ref reader, EventingSerializerContext.Primary.EventingFunctionUrlNoAuth),
                "basic" => JsonSerializer.Deserialize(ref reader, EventingSerializerContext.Primary.EventingFunctionUrlAuthBasic),
                "digest" => JsonSerializer.Deserialize(ref reader, EventingSerializerContext.Primary.EventingFunctionUrlAuthDigest),
                "bearer" => JsonSerializer.Deserialize(ref reader, EventingSerializerContext.Primary.EventingFunctionUrlAuthBearer),
                _ => (ISerializableEventingFunctionUrlAuth?) null
            };
            if (auth is null)
            {
                ThrowHelper.ThrowJsonException("Unsupported auth-type returned.");
            }

            result.Auth = auth;

            return result;
        }

        public override void Write(Utf8JsonWriter writer, EventingFunctionUrlBinding value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value.Auth is ISerializableEventingFunctionUrlAuth serializable)
            {
                serializable.WriteToObject(writer);
            }

            writer.WriteString(HostnamePropertyName, value.Hostname);
            writer.WriteBoolean(AllowCookiesPropertyName, value.AllowCookies);
            writer.WriteBoolean(ValidateSslCertificatePropertyName, value.ValidateSslCertificate);
            writer.WriteString(ValuePropertyName, value.Alias);
            writer.WriteEndObject();
        }
    }
}
