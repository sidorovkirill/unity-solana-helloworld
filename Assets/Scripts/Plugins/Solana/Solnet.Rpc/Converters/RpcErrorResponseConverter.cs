using Newtonsoft.Json;
using Solnet.Rpc.Messages;
using System;

namespace Solnet.Rpc.Converters
{
    /// <summary>
    /// Converts a TransactionError from json into its model representation.
    /// </summary>
    public class RpcErrorResponseConverter : JsonConverter<JsonRpcErrorResponse>
    {
        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="JsonReader"/> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read. If there is no existing value then <c>null</c> will be used.</param>
        /// <param name="hasExistingValue">The existing value has a value.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The object value.</returns>
        public override JsonRpcErrorResponse ReadJson(JsonReader reader, Type objectType, JsonRpcErrorResponse existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject) return null;

            reader.Read();

            var err = new JsonRpcErrorResponse();

            while (reader.TokenType != JsonToken.EndObject)
            {
                var prop = reader.Value.ToString();

                reader.Read();

                if ("jsonrpc" == prop)
                {
                    // do nothing
                }
                else if ("id" == prop)
                {
                    err.Id = (int)reader.ReadAsInt32();
                }
                else if ("error" == prop)
                {
                    if(reader.TokenType == JsonToken.String)
                    {
                        err.ErrorMessage = reader.Value.ToString();;
                    }
                    else if(reader.TokenType == JsonToken.StartObject)
                    {
                        err.Error = JsonConvert.DeserializeObject<ErrorContent>(reader.Value.ToString());
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                else
                {
                    reader.Skip();
                }

                reader.Read();
            }
            return err;
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="JsonWriter"/> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, JsonRpcErrorResponse value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}