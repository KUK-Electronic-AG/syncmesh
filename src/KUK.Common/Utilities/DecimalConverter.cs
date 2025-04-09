using System.Numerics;
using Newtonsoft.Json;

namespace KUK.Common.Utilities
{
    public class DecimalConverter : JsonConverter
    {
        private readonly int _decimalPlaces;

        public DecimalConverter(int decimalPlaces)
        {
            _decimalPlaces = decimalPlaces;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            decimal decimalValue = (decimal)value;
            writer.WriteValue(Math.Round(decimalValue, _decimalPlaces));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Float && reader.TokenType != JsonToken.Integer && reader.TokenType != JsonToken.String)
            {
                throw new JsonSerializationException("Expected float, integer, or string for decimal value.");
            }

            if (reader.TokenType == JsonToken.String)
            {
                var base64String = reader.Value.ToString();
                if (!string.IsNullOrEmpty(base64String))
                {
                    try
                    {
                        return ConvertStringToDecimal(base64String, _decimalPlaces);
                    }
                    catch (FormatException)
                    {
                        throw new JsonSerializationException("Invalid Base64 string for decimal conversion.");
                    }
                }
            }

            decimal decimalValue = Convert.ToDecimal(reader.Value);
            return Math.Round(decimalValue, _decimalPlaces);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal);
        }

        internal decimal ConvertStringToDecimal(string encodedValue, int scale)
        {
            byte[] decodedBytes = Convert.FromBase64String(encodedValue);
            BigInteger bigInteger = new BigInteger(decodedBytes, isUnsigned: true, isBigEndian: true);
            decimal unscaledValue = (decimal)bigInteger;
            decimal divisor = Power(10, scale);
            decimal result = unscaledValue / divisor;
            return Math.Round(result, scale, MidpointRounding.AwayFromZero);
        }

        private decimal Power(decimal baseValue, int exponent)
        {
            decimal result = 1;
            for (int i = 0; i < exponent; i++)
            {
                result *= baseValue;
            }
            return result;
        }
    }
}
