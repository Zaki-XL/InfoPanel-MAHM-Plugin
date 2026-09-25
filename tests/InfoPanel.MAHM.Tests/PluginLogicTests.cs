using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using InfoPanel.Plugins;
using InfoPanel.MAHM.Services;
using static InfoPanel.MAHM.Tests.BoundaryAndExceptionTests;

namespace InfoPanel.MAHM.Tests
{
    public class PluginLogicTests
    {
        [Fact]
        public void Load_Proposal1_SeparatesGpuMainAndAdvancedAndCpuCores()
        {
            var mock = new MockReaderService
            {
                SensorsToReturn = new Dictionary<string, MahmSensorData>(StringComparer.OrdinalIgnoreCase)
                {
                    { "GPU usage", new MahmSensorData { Name = "GPU usage", Value = 45.0f, Units = "%" } },
                    { "FB usage", new MahmSensorData { Name = "FB usage", Value = 12.0f, Units = "%" } },
                    { "Temp limit", new MahmSensorData { Name = "Temp limit", Value = 0f, Units = "" } },
                    { "CPU temperature", new MahmSensorData { Name = "CPU temperature", Value = 60.0f, Units = "°C" } },
                    { "CPU1 usage", new MahmSensorData { Name = "CPU1 usage", Value = 20.0f, Units = "%" } },
                    { "CPU1 temperature", new MahmSensorData { Name = "CPU1 temperature", Value = 58.0f, Units = "°C" } }
                }
            };

            var plugin = new MahmPlugin(mock);
            var containers = new List<IPluginContainer>();
            plugin.Load(containers);

            // コンテナ名の検証 (重複なし)
            Assert.Contains(containers, c => c.Id == "gpu-metrics" && c.Name == "GPU");
            Assert.Contains(containers, c => c.Id == "gpu-advanced-metrics" && c.Name == "GPU - Advanced & Limits");
            Assert.Contains(containers, c => c.Id == "cpu-metrics" && c.Name == "CPU");
            Assert.Contains(containers, c => c.Id == "cpu-cores-metrics" && c.Name == "CPU - Cores");
            Assert.Contains(containers, c => c.Id == "gaming-metrics" && c.Name == "Gaming (RTSS)");

            // GPU Main に GPU usage と FB usage (VRAM Usage) が集約されていること
            var gpuMain = containers.Find(c => c.Id == "gpu-metrics")!;
            Assert.Contains(gpuMain.Entries, e => e.Id == "gpu-usage");
            Assert.Contains(gpuMain.Entries, e => e.Id == "fb-usage");
            var fbEntry = gpuMain.Entries.Find(e => e.Id == "fb-usage")!;
            Assert.Equal("FB usage (VRAM Usage)", fbEntry.Name);

            // GPU Advanced にはリミット系 (Temp limit) があること
            var gpuAdv = containers.Find(c => c.Id == "gpu-advanced-metrics")!;
            Assert.DoesNotContain(gpuAdv.Entries, e => e.Id == "fb-usage");
            Assert.Contains(gpuAdv.Entries, e => e.Id == "temp-limit");

            // CPU Cores に CPU1 usage と CPU1 temperature が集約されていること
            var cpuCores = containers.Find(c => c.Id == "cpu-cores-metrics")!;
            Assert.Contains(cpuCores.Entries, e => e.Id == "cpu1-usage");
            Assert.Contains(cpuCores.Entries, e => e.Id == "cpu1-temperature");
        }

        [Fact]
        public async Task UpdateAsync_WhenAfterburnerNotRunning_SetsAllToNaNAndNa()
        {
            var mock = new MockReaderService
            {
                SensorsToReturn = new Dictionary<string, MahmSensorData>()
            };

            var plugin = new MahmPlugin(mock);
            var containers = new List<IPluginContainer>();
            plugin.Load(containers);

            await plugin.UpdateAsync(CancellationToken.None);

            var gamingContainer = containers.Find(c => c.Id == "gaming-metrics")!;
            var gamingStatus = (IPluginText)gamingContainer.Entries.Find(e => e.Id == "gaming-status")!;
            Assert.Equal("Afterburner Not Running", gamingStatus.Value);

            var fpsText = (IPluginText)gamingContainer.Entries.Find(e => e.Id == "framerate-text")!;
            Assert.Equal("N/A", fpsText.Value);
        }
    }
}
