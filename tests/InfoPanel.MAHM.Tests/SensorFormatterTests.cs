using Xunit;
using InfoPanel.MAHM.Services;

namespace InfoPanel.MAHM.Tests
{
    public class SensorFormatterTests
    {
        [Fact]
        public void Format_WhenValueIsNull_ReturnsNa()
        {
            var result = SensorFormatter.Format(null, "ﾂｰC");
            Assert.Equal("N/A", result);
        }

        [Fact]
        public void Format_WhenValueIsNaN_ReturnsNa()
        {
            var result = SensorFormatter.Format(float.NaN, "%");
            Assert.Equal("N/A", result);
        }

        [Fact]
        public void Format_WhenValueIsPositiveInfinity_ReturnsNa()
        {
            var result = SensorFormatter.Format(float.PositiveInfinity, "W");
            Assert.Equal("N/A", result);
        }

        [Fact]
        public void Format_WhenValueIsFltMax_ReturnsNa()
        {
            // MSI Afterburner / RTSS の未計測センチネル値 (float.MaxValue)
            var result = SensorFormatter.Format(float.MaxValue, "FPS");
            Assert.Equal("N/A", result);
        }

        [Fact]
        public void Format_WhenValueExceedsSentinel_ReturnsNa()
        {
            var result = SensorFormatter.Format(3.4028235E+38f, "FPS");
            Assert.Equal("N/A", result);
        }

        [Theory]
        [InlineData(45.2f, "°C", "45.2 °C")]
        [InlineData(0f, "%", "0.0 %")]
        [InlineData(100f, "%", "100.0 %")]
        public void Format_WithValidValues_ReturnsFormattedStringWithUnit(float value, string unit, string expected)
        {
            var result = SensorFormatter.Format(value, unit, "F1");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FormatInt_WithValidValues_RoundsToInteger()
        {
            var result = SensorFormatter.FormatInt(144.4f, "FPS");
            Assert.Equal("144 FPS", result);
        }

        [Fact]
        public void Format_WhenUnitIsEmpty_ReturnsNumberOnly()
        {
            var result = SensorFormatter.Format(42.5f, "", "F1");
            Assert.Equal("42.5", result);
        }
    }
}
