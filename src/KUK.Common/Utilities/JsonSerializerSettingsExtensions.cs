using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace KUK.Common.Utilities
{
    public static class JsonSerializerSettingsExtensions
    {
        public static void ConfigureConverters(this JsonSerializerSettings settings)
        {
            settings.ContractResolver = new CustomContractResolver();
        }

        private class CustomContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                var unixEpochAttribute = member.GetCustomAttribute<UnixEpochDateTimeConverterAttribute>();
                if (unixEpochAttribute != null)
                {
                    property.Converter = new UnixEpochDateTimeConverter(unixEpochAttribute.Precision);
                }

                var decimalAttribute = member.GetCustomAttribute<DecimalConverterAttribute>();
                if (decimalAttribute != null)
                {
                    property.Converter = new DecimalConverter(decimalAttribute.DecimalPlaces);
                }

                return property;
            }
        }
    }
}
