using System;
using InfoPanel.MAHM.Services;
using InfoPanel.MAHM;
using Xunit;

namespace InfoPanel.MAHM.Tests
{
    public class AfterburnerConfigServiceTests
    {
        [Theory]
        [InlineData("HwPollPeriod=500", 500)]
        [InlineData("HwPollPeriod=1000", 1000)]
        [InlineData("HwPollPeriod=250", 250)]
        [InlineData("HwPollPeriod = 2000", 2000)]
        [InlineData("[Settings]\r\nLanguage=JP\r\nHwPollPeriod=750\r\nLockProfiles=1", 750)]
        public void ParsePollingPeriod_ValidInputs_ReturnsParsedMilliseconds(string configText, int expectedMs)
        {
            int result = AfterburnerConfigService.ParsePollingPeriod(configText);
            Assert.Equal(expectedMs, result);
        }

        [Theory]
        [InlineData("HwPollPeriod=0", 1000)]
        [InlineData("HwPollPeriod=50", 1000)]
        [InlineData("HwPollPeriod=-500", 1000)]
        [InlineData("HwPollPeriod=20000", 1000)]
        [InlineData("HwPollPeriod=abc", 1000)]
        [InlineData("", 1000)]
        [InlineData("OtherKey=1000", 1000)]
        public void ParsePollingPeriod_OutOfRangeOrInvalid_ReturnsDefault1000Ms(string configText, int expectedMs)
        {
            int result = AfterburnerConfigService.ParsePollingPeriod(configText);
            Assert.Equal(expectedMs, result);
        }

        [Fact]
        public void MahmPlugin_WithCustomConfigService_ReflectsPollingInterval()
        {
            var mockConfig = new MockConfigService(TimeSpan.FromMilliseconds(500));
            var mockReader = new MockMahmReaderService();

            var plugin = new MahmPlugin(mockReader, mockConfig);

            Assert.Equal(TimeSpan.FromMilliseconds(500), plugin.UpdateInterval);
        }

        [Fact]
        public void RealEnvironment_ReadsLocalAfterburnerConfigSuccessfully()
        {
            var service = new AfterburnerConfigService();
            var interval = service.GetPollingInterval();

            Assert.True(interval.TotalMilliseconds >= 100 && interval.TotalMilliseconds <= 10000);
            Assert.Equal(1000, interval.TotalMilliseconds);
        }

        private class MockConfigService : IAfterburnerConfigService
        {
            private readonly TimeSpan _interval;
            public MockConfigService(TimeSpan interval) => _interval = interval;
            public TimeSpan GetPollingInterval() => _interval;
        }

        private class MockMahmReaderService : IMahmReaderService
        {
            public bool IsConnected => true;
            public bool Connect() => true;
            public void Disconnect() { }
            public System.Collections.Generic.Dictionary<string, MahmSensorData> ReadAllSensors() => new();
            public void Dispose() { }
        }
    }
}