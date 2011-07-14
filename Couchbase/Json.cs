using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace Couchbase
{
	internal class Json
	{
		public static object Parse(JsonReader reader)
		{
			return CreateValue(reader);
		}

		public static object Parse(TextReader reader)
		{
			var json = new JsonTextReader(reader);

			ReadChecked(json);

			return CreateValue(json);
		}

		static void ReadChecked(JsonReader reader)
		{
			if (!reader.Read())
				throw NewInvalidOperationException("Unexpected EOS", reader);
		}

		static Exception NewInvalidOperationException(string message, JsonReader reader)
		{
			var lineInfo = reader as IJsonLineInfo;

			if (lineInfo != null && lineInfo.HasLineInfo())
				message += String.Format(" @ {0}:{1}", lineInfo.LineNumber, lineInfo.LinePosition);

			return new InvalidOperationException(message);
		}

		static object CreateValue(JsonReader reader)
		{
		Restart:
			switch (reader.TokenType)
			{
				case JsonToken.StartObject: return CreateObject(reader);
				case JsonToken.StartArray: return CreateArray(reader);

				case JsonToken.StartConstructor:
				case JsonToken.EndConstructor:
					return reader.Value.ToString();

				case JsonToken.Comment:
					ReadChecked(reader);
					goto Restart;

				case JsonToken.Raw:
				case JsonToken.Integer:
				case JsonToken.Float:
				case JsonToken.Boolean:
				case JsonToken.Date:
				case JsonToken.String:
				case JsonToken.Bytes:
					return reader.Value;

				case JsonToken.Null:
				case JsonToken.Undefined:
					return null;

				default: throw NewInvalidOperationException("Unexpected token: " + reader.TokenType, reader);
			}
		}

		private static object CreateArray(JsonReader reader)
		{
			var retval = new List<object>();

			while (reader.Read())
			{
				if (reader.TokenType == JsonToken.EndArray)
					return retval.ToArray();

				retval.Add(CreateValue(reader));
			}

			throw NewInvalidOperationException("End of array missing", reader);
		}

		private static object CreateObject(JsonReader reader)
		{
			var retval = new Dictionary<string, object>();

			while (reader.Read())
			{
				if (reader.TokenType == JsonToken.PropertyName)
				{
					var name = (string)reader.Value;

					ReadChecked(reader);

					var value = CreateValue(reader);
					retval[name] = value;
				}
				else if (reader.TokenType == JsonToken.EndObject)
					return retval;
			}

			throw NewInvalidOperationException("End of object missing", reader);
		}
	}
}
