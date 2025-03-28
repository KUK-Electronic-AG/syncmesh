using KUK.Common.Utilities;

namespace KUK.UnitTests
{
    public class UnixEpochDateTimeConverterTests
    {
        [Theory]

        [InlineData(631152000000, 0, "1990-01-01T00:00:00Z")] // 1 January 1990, midnight
        [InlineData(632361600000, 0, "1990-01-15T00:00:00Z")] // 15 January 1990, midnight
        [InlineData(646704000000, 0, "1990-06-30T00:00:00Z")] // 30 June 1990, midnight
        [InlineData(631801212000, 3, "1990-01-08T12:20:12.000Z")] // 8 January 1990, 12:20:12
        [InlineData(632060425000, 3, "1990-01-11T12:20:25.000Z")] // 11 January 1990, 12:20:25
        [InlineData(646143612000, 3, "1990-06-23T12:20:12.000Z")] // 23 June 1990, 12:20:12
        [InlineData(631542012345000, 6, "1990-01-05T12:20:12.345000Z")] // 5 January 1990, 12:20:12.345
        [InlineData(632665212345000, 6, "1990-01-18T12:20:12.345000Z")] // 18 January 1990, 12:20:12.345
        [InlineData(645798012345000, 6, "1990-06-19T12:20:12.345000Z")] // 19 June 1990, 12:20:12.345

        [InlineData(1704067200000, 0, "2024-01-01T00:00:00Z")] // 1 January 2024, midnight
        [InlineData(1705276800000, 0, "2024-01-15T00:00:00Z")] // 15 January 2024, midnight
        [InlineData(1719705600000, 0, "2024-06-30T00:00:00Z")] // 30 June 2024, midnight
        [InlineData(1704716412000, 3, "2024-01-08T12:20:12.000Z")] // 8 January 2024, 12:20:12
        [InlineData(1705062025000, 3, "2024-01-12T12:20:25.000Z")] // 12 January 2024, 12:20:25
        [InlineData(1719145212000, 3, "2024-06-23T12:20:12.000Z")] // 23 June 2024, 12:20:12
        [InlineData(1704457212345000, 6, "2024-01-05T12:20:12.345000Z")] // 5 January 2024, 12:20:12.345
        [InlineData(1705580412345000, 6, "2024-01-18T12:20:12.345000Z")] // 18 January 2024, 12:20:12.345
        [InlineData(1718799612345000, 6, "2024-06-19T12:20:12.345000Z")] // 19 June 2024, 12:20:12.345

        [InlineData(2524608000000, 0, "2050-01-01T00:00:00Z")] // 1 January 2050, midnight
        [InlineData(2525817600000, 0, "2050-01-15T00:00:00Z")] // 15 January 2050, midnight
        [InlineData(2540160000000, 0, "2050-06-30T00:00:00Z")] // 30 June 2050, midnight
        [InlineData(2525257212000, 3, "2050-01-08T12:20:12.000Z")] // 8 January 2050, 12:20:12
        [InlineData(2525516425000, 3, "2050-01-11T12:20:25.000Z")] // 11 January 2050, 12:20:25
        [InlineData(2539599612000, 3, "2050-06-23T12:20:12.000Z")] // 23 June 2050, 12:20:12
        [InlineData(2524998012345000, 6, "2050-01-05T12:20:12.345000Z")] // 5 January 2050, 12:20:12.345
        [InlineData(2526121212345000, 6, "2050-01-18T12:20:12.345000Z")] // 18 January 2050, 12:20:12.345
        [InlineData(2539254012345000, 6, "2050-06-19T12:20:12.345000Z")] // 19 June 2050, 12:20:12.345

        public void ConvertIntToDateTime_ShouldReturnExpectedValue(long valueSinceEpoch, int precision, string expectedDateTimeString)
        {
            // Arrange
            UnixEpochDateTimeConverter converter = new UnixEpochDateTimeConverter(precision);
            DateTime expectedDateTime = DateTime.Parse(expectedDateTimeString).ToUniversalTime();

            // Act
            DateTime actualDateTime = converter.ConvertIntToDateTime(valueSinceEpoch, precision);

            // Assert
            Assert.Equal(expectedDateTime, actualDateTime);
        }
    }
}
