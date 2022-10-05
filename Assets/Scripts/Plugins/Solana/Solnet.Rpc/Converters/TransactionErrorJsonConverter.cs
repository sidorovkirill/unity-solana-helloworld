using Newtonsoft.Json;
using Solnet.Rpc.Models;
using System;

namespace Solnet.Rpc.Converters
{
    /// <summary>
    /// Converts a TransactionError from json into its model representation.
    /// </summary>
    public class TransactionErrorJsonConverter : JsonConverter<TransactionError>
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
        public override TransactionError ReadJson(JsonReader reader, Type objectType, TransactionError existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var err = new TransactionError();

            if (reader.TokenType == JsonToken.String)
            {
                var enumValue = reader.Value.ToString();

                Enum.TryParse(enumValue, ignoreCase: false, out TransactionErrorType errorType);
                err.Type = errorType;
                return err;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonException("Unexpected error value.");
            }

            reader.Read();

            if (reader.TokenType != JsonToken.PropertyName)
            {
                throw new JsonException("Unexpected error value.");
            }


            {
                var enumValue = reader.Value.ToString();
                Enum.TryParse(enumValue, ignoreCase: false, out TransactionErrorType errorType);
                err.Type = errorType;
            }

            reader.Read();
            err.InstructionError = new InstructionError();

            if (reader.TokenType != JsonToken.StartArray)
            {
                throw new JsonException("Unexpected error value.");
            }

            reader.Read();

            if ( reader.TokenType != JsonToken.Float || reader.TokenType != JsonToken.Integer )
            {
                throw new JsonException("Unexpected error value.");
            }

            err.InstructionError.InstructionIndex = (int)reader.ReadAsInt32();

            reader.Read();

            if (reader.TokenType == JsonToken.String)
            {
                var enumValue = reader.Value.ToString();

                Enum.TryParse(enumValue, ignoreCase: false, out InstructionErrorType errorType);
                err.InstructionError.Type = errorType;
                reader.Read(); //string

                reader.Read(); //endarray
                return err;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonException("Unexpected error value.");
            }

            reader.Read();


            if (reader.TokenType != JsonToken.PropertyName)
            {
                throw new JsonException("Unexpected error value.");
            }
            {
                var enumValue = reader.Value.ToString();
                Enum.TryParse(enumValue, ignoreCase: false, out InstructionErrorType errorType);
                err.InstructionError.Type = errorType;
            }

            reader.Read();

            if ( reader.TokenType != JsonToken.Float || reader.TokenType != JsonToken.Integer)
            {
                err.InstructionError.CustomError = (uint)reader.ReadAsInt32();
                reader.Read(); //number
                reader.Read(); //endobj
                reader.Read(); //endarray

                return err;
            }

            if (reader.TokenType != JsonToken.String)
            {
                throw new JsonException("Unexpected error value.");
            }

            err.InstructionError.BorshIoError = reader.Value.ToString();
            reader.Read(); //string
            reader.Read(); //endobj
            reader.Read(); //endarray

            return err;
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="JsonWriter"/> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, TransactionError value, JsonSerializer serializer)
        {
            if (value.InstructionError != null)
            {

                // looking to output something like this...
                // { 'InstructionError': [0, 'InvalidAccountData'] }
                writer.WriteStartObject();
                writer.WritePropertyName("InstructionError");

                // innards
                var enumName = value.InstructionError.Type.ToString();
                writer.WriteStartArray();
                writer.WriteValue(value.InstructionError.InstructionIndex);
                writer.WriteValue(enumName);
                writer.WriteEndArray();

                writer.WriteEndObject();

            }
            else
                throw new NotImplementedException();
        }
    }
}