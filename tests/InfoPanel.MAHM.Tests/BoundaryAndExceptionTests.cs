using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using InfoPanel.Plugins;
using InfoPanel.MAHM.Native;
using InfoPanel.MAHM.Services;

namespace InfoPanel.MAHM.Tests
{
    public class BoundaryAndExceptionTests
    {
        #region 1. SensorFormatter 異常系・境界テスト

        [Fact]
        public void SensorFormatter_NegativeInfinity_ReturnsNa()
        {
            var res = SensorFormatter.Format(float.NegativeInfinity, "°C");
            Assert.Equal("N/A", res);
        }

        [Fact]
        public void SensorFormatter_ExtremeValues_FormattedCorrectly()
        {
            // 極端に巨大な正常値 (1e10f) でも例外が出ず正しくフォーマットされること
            var resHuge = SensorFormatter.Format(1e10f, "MB", "F0");
            Assert.NotEqual("N/A", resHuge);
            Assert.Contains("MB", resHuge);

            // 一方で、Afterburner の FLT_MAX センチネル (float.MaxValue / MinValue) は安全に N/A になること
            var resMax = SensorFormatter.Format(float.MaxValue, "MB", "F0");
            Assert.Equal("N/A", resMax);

            var resMin = SensorFormatter.Format(float.MinValue, "MB", "F0");
            Assert.Equal("N/A", resMin);
        }

        [Fact]
        public void SensorFormatter_NegativeTemperature_FormattedCorrectly()
        {
            // 氷点下などの負の温度
            var res = SensorFormatter.Format(-15.4f, "°C", "F1");
            Assert.Equal("-15.4 °C", res);
        }

        [Fact]
        public void SensorFormatter_MicroValues_RoundsGracefully()
        {
            // 非常に微小な値 (0.00001f)
            var res = SensorFormatter.Format(0.00001f, "V", "F2");
            Assert.Equal("0.00 V", res);
        }

        #endregion

        #region 2. BufferParser 境界・破損耐性テスト

        [Fact]
        public void BufferParser_ZeroEntrySize_DoesNotInfiniteLoopAndReturnsEmpty()
        {
            var header = new MAHM_SHARED_MEMORY_HEADER
            {
                dwSignature = MahmBufferParser.ExpectedSignature,
                dwHeaderSize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_HEADER>(),
                dwNumEntries = 10,
                dwEntrySize = 0 // 異常値
            };

            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(header));
            try
            {
                Marshal.StructureToPtr(header, p, false);
                var dict = MahmBufferParser.ParseEntries(p, header);
                Assert.Empty(dict);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        [Fact]
        public void BufferParser_ZeroEntries_ReturnsEmptyDictionary()
        {
            var header = new MAHM_SHARED_MEMORY_HEADER
            {
                dwSignature = MahmBufferParser.ExpectedSignature,
                dwHeaderSize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_HEADER>(),
                dwNumEntries = 0,
                dwEntrySize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_ENTRY>()
            };

            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(header));
            try
            {
                Marshal.StructureToPtr(header, p, false);
                var dict = MahmBufferParser.ParseEntries(p, header);
                Assert.Empty(dict);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        [Fact]
        public void BufferParser_CorruptedEntry_SkipsAndParsesValidEntries()
        {
            var header = new MAHM_SHARED_MEMORY_HEADER
            {
                dwSignature = MahmBufferParser.ExpectedSignature,
                dwHeaderSize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_HEADER>(),
                dwNumEntries = 2,
                dwEntrySize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_ENTRY>()
            };

            int total = (int)header.dwHeaderSize + (int)(header.dwNumEntries * header.dwEntrySize);
            IntPtr p = Marshal.AllocHGlobal(total);
            try
            {
                Marshal.StructureToPtr(header, p, false);

                // Entry 0: 正常
                var e0 = new MAHM_SHARED_MEMORY_ENTRY { szSrcName = "GPU usage", data = 42.0f, szSrcUnits = "%" };
                IntPtr p0 = IntPtr.Add(p, (int)header.dwHeaderSize);
                Marshal.StructureToPtr(e0, p0, false);

                // Entry 1: 空の名前
                var e1 = new MAHM_SHARED_MEMORY_ENTRY { szSrcName = "", data = 0.0f };
                IntPtr p1 = IntPtr.Add(p0, (int)header.dwEntrySize);
                Marshal.StructureToPtr(e1, p1, false);

                var dict = MahmBufferParser.ParseEntries(p, header);
                Assert.Single(dict);
                Assert.True(dict.ContainsKey("GPU usage"));
                Assert.Equal(42.0f, dict["GPU usage"].Value);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        #endregion

        #region 3. MahmPlugin 異常系・例外耐性・スレッドセーフテスト

        public class MockReaderService : IMahmReaderService
        {
            public bool ConnectResult { get; set; } = true;
            public bool IsConnected => ConnectResult;
            public Exception? ReadException { get; set; }
            public Dictionary<string, MahmSensorData> SensorsToReturn { get; set; } = new();

            public bool Connect() => ConnectResult;
            public void Disconnect() { }
            public Dictionary<string, MahmSensorData> ReadAllSensors()
            {
                if (ReadException != null) throw ReadException;
                return SensorsToReturn;
            }
            public void Dispose() { }
        }

        [Fact]
        public async Task Plugin_WhenReaderThrowsException_DoesNotCrashAndSetsErrorStatus()
        {
            var mock = new MockReaderService
            {
                ReadException = new InvalidOperationException("Simulated shared memory read failure")
            };

            var plugin = new MahmPlugin(mock);
            var containers = new List<IPluginContainer>();
            plugin.Load(containers);

            var ex = await Record.ExceptionAsync(() => plugin.UpdateAsync(CancellationToken.None));
            Assert.Null(ex); // 例外でクラッシュしないこと

            var gpuContainer = containers.Find(c => c.Id == "gpu-metrics")!;
            var status = (IPluginText)gpuContainer.Entries.Find(e => e.Id == "gpu-status")!;
            Assert.Contains("Error:", status.Value);
        }

        [Fact]
        public async Task Plugin_PartialMetricsMissing_FillsOnlyPresentAndSetsRestToNa()
        {
            var mock = new MockReaderService
            {
                SensorsToReturn = new Dictionary<string, MahmSensorData>(StringComparer.OrdinalIgnoreCase)
                {
                    { "GPU temperature", new MahmSensorData { Name = "GPU temperature", Value = 52.0f, Units = "°C" } },
                    { "Fan speed", new MahmSensorData { Name = "Fan speed", Value = 1200f, Units = "RPM" } }
                }
            };

            var plugin = new MahmPlugin(mock);
            var containers = new List<IPluginContainer>();
            plugin.Load(containers);

            // Update 時に Fan speed が消失（欠損）した場合
            mock.SensorsToReturn = new Dictionary<string, MahmSensorData>(StringComparer.OrdinalIgnoreCase)
            {
                { "GPU temperature", new MahmSensorData { Name = "GPU temperature", Value = 55.0f, Units = "°C" } }
            };

            await plugin.UpdateAsync(CancellationToken.None);

            var gpuContainer = containers.Find(c => c.Id == "gpu-metrics")!;
            var tempSensor = (IPluginSensor)gpuContainer.Entries.Find(e => e.Id == "gpu-temperature")!;
            Assert.Equal(55.0f, tempSensor.Value);

            var fanSensor = (IPluginSensor)gpuContainer.Entries.Find(e => e.Id == "fan-speed")!;
            Assert.NotNull(fanSensor);
            Assert.True(float.IsNaN(fanSensor.Value));

            var fanText = (IPluginText)gpuContainer.Entries.Find(e => e.Id == "fan-speed-text")!;
            Assert.NotNull(fanText);
            Assert.Equal("N/A", fanText.Value);
        }

        [Fact]
        public async Task Plugin_ConcurrentUpdateCalls_ThreadSafe()
        {
            var mock = new MockReaderService
            {
                SensorsToReturn = new Dictionary<string, MahmSensorData>(StringComparer.OrdinalIgnoreCase)
                {
                    { "GPU temperature", new MahmSensorData { Name = "GPU temperature", Value = 45.0f, Units = "°C" } }
                }
            };

            var plugin = new MahmPlugin(mock);
            var containers = new List<IPluginContainer>();
            plugin.Load(containers);

            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() => plugin.UpdateAsync(CancellationToken.None)));
            }

            var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
            Assert.Null(ex);
        }

        #endregion
    }
}
