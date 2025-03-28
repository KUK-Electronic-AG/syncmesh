using KUK.Common.Utilities;

namespace KUK.UnitTests
{
    public class DecimalConverterTests
    {
        [Theory]
        [InlineData("JxA=", 2, 100.00)] // Total
        [InlineData("ew==", 0, 123.00)] // TestValue0
        [InlineData("BNI=", 1, 123.4)] // TestValue1
        [InlineData("MDk=", 2, 123.45)] // TestValue2
        [InlineData("AeJA", 3, 123.456)] // TestValue3
        [InlineData("EtaH", 4, 123.4567)] // TestValue4
        [InlineData("ALxhTg==", 5, 123.45678)] // TestValue5
        [InlineData("B1vNFQ==", 6, 123.456789)] // TestValue6
        [InlineData("SZYC0w==", 7, 123.4567891)] // TestValue7
        [InlineData("AQ==", 1, 0.1)] // TestValue1
        [InlineData("AQ==", 2, 0.01)] // TestValue2
        [InlineData("AQ==", 3, 0.001)] // TestValue3
        [InlineData("AQ==", 4, 0.0001)] // TestValue4
        [InlineData("AQ==", 5, 0.00001)] // TestValue5
        [InlineData("AQ==", 6, 0.000001)] // TestValue6
        [InlineData("AQ==", 7, 0.0000001)] // TestValue7
        [InlineData("AQ==", 8, 0.00000001)] // TestValue8
        public void ConvertStringToDecimal_ShouldReturnExpectedValue(string encodedValue, int scale, decimal expectedValue)
        {
            // Act
            DecimalConverter converter = new DecimalConverter(scale);
            decimal actualValue = converter.ConvertStringToDecimal(encodedValue, scale);

            // Assert
            Assert.Equal(expectedValue, actualValue);
        }
    }
}