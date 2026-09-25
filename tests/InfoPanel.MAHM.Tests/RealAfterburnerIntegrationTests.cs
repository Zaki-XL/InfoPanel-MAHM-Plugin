using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using InfoPanel.Plugins;
using InfoPanel.MAHM.Services;

namespace InfoPanel.MAHM.Tests
{
    public class RealAfterburnerIntegrationTests
    {
        private static bool IsAfterburnerRunning()
        {
            return Process.GetProcessesByName("MSIAfterburner").Length > 0;
        }

        [Fact]
        public void RealAfterburner_CanConnectAndReadSensors()
        {
            if (!IsAfterburnerRunning()) return;

            using var service = new MahmReaderService();
            bool connected = service.Connect();
            Assert.True(connected);

            var sensors = service.ReadAllSensors();
            Assert.True(sensors.Count >= 10, $"Actual: {sensors.Count}");
            Assert.True(sensors.ContainsKey("GPU temperature"));
        }

        [Fact]
        public void RealAfterburner_PopulatesProposal1ContainersCleanly()
        {
            if (!IsAfterburnerRunning()) return;

            using var service = new MahmReaderService();
            var plugin = new MahmPlugin(service);
            var containers = new List<IPluginContainer>();
            plugin.Load(containers);

            // 実機に存在する基本コンテナが美しく生成されていること
            Assert.Contains(containers, c => c.Id == "gpu-metrics" && c.Name == "GPU");
            Assert.Contains(containers, c => c.Id == "gpu-advanced-metrics" && c.Name == "GPU - Advanced & Limits");
            Assert.Contains(containers, c => c.Id == "cpu-metrics" && c.Name == "CPU");
            Assert.Contains(containers, c => c.Id == "memory-metrics" && c.Name == "Memory");
            Assert.Contains(containers, c => c.Id == "gaming-metrics" && c.Name == "Gaming (RTSS)");

            // 実機 GPU コンテナに GPU temperature および FB usage (VRAM Usage) が含まれること
            var gpuMain = containers.Find(c => c.Id == "gpu-metrics")!;
            Assert.Contains(gpuMain.Entries, e => e.Id == "gpu-temperature");
            Assert.Contains(gpuMain.Entries, e => e.Id == "fb-usage");
            var fbEntry = gpuMain.Entries.Find(e => e.Id == "fb-usage")!;
            Assert.Equal("FB usage (VRAM Usage)", fbEntry.Name);

            // 実機 Gaming コンテナに Framerate Min/Avg/Max が分類されていること
            var gaming = containers.Find(c => c.Id == "gaming-metrics")!;
            Assert.Contains(gaming.Entries, e => e.Id == "framerate");
        }

        [Fact]
        public async Task RealAfterburner_PluginUpdatesRealValues()
        {
            if (!IsAfterburnerRunning()) return;

            using var service = new MahmReaderService();
            var plugin = new MahmPlugin(service);
            var containers = new List<IPluginContainer>();
            plugin.Load(containers);

            await plugin.UpdateAsync(CancellationToken.None);

            var gpuContainer = containers.Find(c => c.Id == "gpu-metrics")!;
            var tempSensor = (IPluginSensor)gpuContainer.Entries.Find(e => e.Id == "gpu-temperature")!;
            Assert.False(float.IsNaN(tempSensor.Value));

            var tempText = (IPluginText)gpuContainer.Entries.Find(e => e.Id == "gpu-temperature-text")!;
            Assert.NotEqual("N/A", tempText.Value);
        }

        [Fact]
        public async Task RealAfterburner_WhenNoGameRunning_FramerateIsNaAndStatusIsIdle()
        {
            if (!IsAfterburnerRunning()) return;

            using var service = new MahmReaderService();
            var plugin = new MahmPlugin(service);
            var containers = new List<IPluginContainer>();
            plugin.Load(containers);

            await plugin.UpdateAsync(CancellationToken.None);

            var gamingContainer = containers.Find(c => c.Id == "gaming-metrics")!;
            
            // Gaming Status が Idle であること（3.4E+38 で誤って Active にならないこと）
            var gamingStatus = (IPluginText)gamingContainer.Entries.Find(e => e.Id == "gaming-status")!;
            Assert.Equal("Idle (No Game Detected)", gamingStatus.Value);

            // Framerate センサーが NaN かつ 表示テキストが N/A であること
            var fpsSensor = (IPluginSensor)gamingContainer.Entries.Find(e => e.Id == "framerate")!;
            Assert.True(float.IsNaN(fpsSensor.Value), $"Expected NaN but got {fpsSensor.Value}");

            var fpsText = (IPluginText)gamingContainer.Entries.Find(e => e.Id == "framerate-text")!;
            Assert.Equal("N/A", fpsText.Value);

            // Framerate Min/Avg/Max も存在していれば N/A であること
            foreach (var entry in gamingContainer.Entries)
            {
                if (entry is IPluginText textEntry && textEntry.Id.StartsWith("framerate-"))
                {
                    Assert.Equal("N/A", textEntry.Value);
                }
            }
        }
    }
}
