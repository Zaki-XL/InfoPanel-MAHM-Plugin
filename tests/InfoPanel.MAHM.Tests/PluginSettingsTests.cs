using Xunit;
using InfoPanel.MAHM.Services;

namespace InfoPanel.MAHM.Tests
{
    public class PluginSettingsTests
    {
        [Theory]
        [InlineData("NaText=\"-\"", "-")]
        [InlineData("NaText=\"--\"", "--")]
        [InlineData("NaText=\"N/A\"", "N/A")]
        [InlineData("NaText=\"[未計測]\"", "[未計測]")]
        public void ParseNaText_WithDoubleQuotes_StripsQuotes(string config, string expected)
        {
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseNaText_WithEmptyQuotes_ReturnsEmptyString()
        {
            string config = "NaText=\"\"";
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal("", result);
        }

        [Fact]
        public void ParseNaText_WithSpacesInsideQuotes_PreservesSpaces()
        {
            string config = "NaText=\" - \"";
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal(" - ", result);
        }

        [Theory]
        [InlineData("NaText=”--”", "--")]
        [InlineData("NaText=“--”", "--")]
        public void ParseNaText_WithFullWidthQuotes_StripsQuotes(string config, string expected)
        {
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseNaText_WithoutQuotes_ReturnsTrimmedValue()
        {
            string config = "NaText=--";
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal("--", result);
        }

        [Fact]
        public void ParseNaText_WithNanTextKey_ParsesCorrectly()
        {
            string config = "NanText=\"NaN\"";
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal("NaN", result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("; This is a comment\n# Another comment")]
        [InlineData("[PluginInfo]\nName=MSI Afterburner Monitor")]
        public void ParseNaText_WhenMissingOrEmpty_ReturnsDefault(string config)
        {
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal(PluginSettings.DefaultNaText, result);
        }

        [Fact]
        public void ParseNaText_WithInlineCommentOutsideQuotes_StripsComment()
        {
            string config = "NaText=-- ; this is comment";
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal("--", result);
        }

        [Fact]
        public void ParseNaText_WithSemicolonInsideQuotes_PreservesSemicolon()
        {
            string config = "NaText=\";)\"";
            var result = PluginSettings.ParseNaText(config);
            Assert.Equal(";)", result);
        }

        [Fact]
        public void PluginSettings_ConstructorWithNull_UsesDefault()
        {
            var settings = new PluginSettings(null);
            Assert.Equal(PluginSettings.DefaultNaText, settings.NaText);
        }

        [Fact]
        public void PluginSettings_ConstructorWithValue_AssignsValue()
        {
            var settings = new PluginSettings("--", UnmeasuredValueMode.Zero);
            Assert.Equal("--", settings.NaText);
            Assert.Equal(UnmeasuredValueMode.Zero, settings.UnmeasuredValueMode);
            Assert.Equal(0.0f, settings.UnmeasuredSensorValue);
        }

        [Theory]
        [InlineData("UnmeasuredValue=0", UnmeasuredValueMode.Zero, 0.0f)]
        [InlineData("UnmeasuredValue=1", UnmeasuredValueMode.MinusOne, -1.0f)]
        [InlineData("UnmeasuredValue=2", UnmeasuredValueMode.NaN, float.NaN)]
        [InlineData("UnmeasuredValue=\"0\"", UnmeasuredValueMode.Zero, 0.0f)]
        [InlineData("UnmeasuredValue=\"1\"", UnmeasuredValueMode.MinusOne, -1.0f)]
        [InlineData("UnmeasuredValue=\"2\"", UnmeasuredValueMode.NaN, float.NaN)]
        [InlineData("UnmeasuredValue=99", UnmeasuredValueMode.NaN, float.NaN)]
        [InlineData("", UnmeasuredValueMode.NaN, float.NaN)]
        public void ParseUnmeasuredValue_ParsesCorrectly(string config, UnmeasuredValueMode expectedMode, float expectedValue)
        {
            var mode = PluginSettings.ParseUnmeasuredValue(config);
            Assert.Equal(expectedMode, mode);

            var settings = new PluginSettings("N/A", mode);
            if (float.IsNaN(expectedValue))
            {
                Assert.True(float.IsNaN(settings.UnmeasuredSensorValue));
            }
            else
            {
                Assert.Equal(expectedValue, settings.UnmeasuredSensorValue);
            }
        }
    }
}
