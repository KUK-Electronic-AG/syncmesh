namespace KUK.Common.Utilities
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UnixEpochDateTimeConverterAttribute : Attribute
    {
        public int Precision { get; }

        public UnixEpochDateTimeConverterAttribute(int precision)
        {
            Precision = precision;
        }
    }
}
