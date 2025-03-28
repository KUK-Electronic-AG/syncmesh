namespace KUK.Common.Utilities
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DecimalConverterAttribute : Attribute
    {
        public int DecimalPlaces { get; }

        public DecimalConverterAttribute(int decimalPlaces)
        {
            DecimalPlaces = decimalPlaces;
        }
    }
}
