using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace KUK.Common.Utilities
{
    public class UnixEpochDateTimeConverter : DateTimeConverterBase
    {
        private readonly int _precision;
        private const string DateTimeFormatString = "yyyy-MM-dd HH:mm:ss.ffffff"; // Define the format string

        public UnixEpochDateTimeConverter(int precision)
        {
            _precision = precision;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DateTime dateTime = (DateTime)value;
            long millisecondsSinceEpoch = new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
            writer.WriteValue(millisecondsSinceEpoch);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    long valueSinceEpoch = (long)reader.Value;
                    return ConvertIntToDateTime(valueSinceEpoch, _precision);

                case JsonToken.String:
                    string dateString = reader.Value.ToString();
                    if (DateTime.TryParseExact(dateString, DateTimeFormatString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDateTime))
                    {
                        return parsedDateTime;
                    }
                    else
                    {
                        throw new JsonSerializationException($"Could not parse DateTime string '{dateString}' with format '{DateTimeFormatString}' or as Unix epoch time.");
                    }

                case JsonToken.Date:
                    return (DateTime)reader.Value;

                default:
                    throw new JsonSerializationException($"Expected integer for Unix epoch time or string for DateTime format. TokenType = {reader.TokenType}");
            }
        }

        internal DateTime ConvertIntToDateTime(long valueSinceEpoch, int precision)
        {
            DateTimeOffset dateTimeOffset;

            switch (precision)
            {
                case 0:
                case 3:
                    dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(valueSinceEpoch);
                    break;
                case 6:
                    dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(valueSinceEpoch / 1000);
                    int remainingMicroseconds = (int)(valueSinceEpoch % 1000);
                    dateTimeOffset = dateTimeOffset.AddTicks(remainingMicroseconds * 10);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(precision), "Unsupported precision value");
            }

            return dateTimeOffset.DateTime;
        }
    }
}