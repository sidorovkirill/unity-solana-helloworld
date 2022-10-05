using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;


namespace Solnet.Rpc.Converters
{
	/// <inheritdoc/>
	public class AccountDataConverter : JsonConverter<List<string>>
	{
		/// <inheritdoc/>
		public override List<string> ReadJson(JsonReader reader, Type objectType, List<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.StartArray)
				return serializer.Deserialize<List<string>>(reader);

			if(reader.TokenType == JsonToken.StartObject)
			{
				var token = JToken.FromObject(reader);
				var jsonAsString = token.Root.ToString();

				return new List<string>() { jsonAsString, "jsonParsed" };
			}

			throw new JsonException("Unable to parse account data");
		}

		/// <inheritdoc/>
		public override void WriteJson(JsonWriter writer, List<string> value, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}
	}
}