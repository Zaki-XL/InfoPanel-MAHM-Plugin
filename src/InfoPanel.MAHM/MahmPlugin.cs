using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InfoPanel.Plugins;
using InfoPanel.MAHM.Services;

namespace InfoPanel.MAHM
{
    public class DynamicMetricPair
    {
        public string SourceName { get; }
        public string Unit { get; }
        public string NumberFormat { get; }
        public PluginSensor Sensor { get; }
        public PluginText Text { get; }

        public DynamicMetricPair(string id, string name, string sourceName, string unit, string numberFormat)
        {
            SourceName = sourceName;
            Unit = unit;
            NumberFormat = numberFormat;
            Sensor = new PluginSensor(id, name, float.NaN, unit);
            Text = new PluginText(id + "-text", name + " (Display)", SensorFormatter.NaText);
        }

        public void Update(float? value)
        {
            if (value.HasValue && !SensorFormatter.IsInvalid(value.Value))
            {
                Sensor.Value = value.Value;
                Text.Value = SensorFormatter.Format(value.Value, Unit, NumberFormat);
            }
            else
            {
                Sensor.Value = float.NaN;
                Text.Value = SensorFormatter.NaText;
            }
        }
    }

    public class MahmPlugin : BasePlugin
    {
        private readonly IMahmReaderService _readerService;
        private readonly object _lock = new();

        private readonly List<DynamicMetricPair> _metrics = new();
        private readonly List<IPluginContainer> _containers = new();

        // ステータス表示
        private readonly PluginText _gpuStatus = new("gpu-status", "GPU Status", "Disconnected");
        private readonly PluginText _cpuStatus = new("cpu-status", "CPU Status", "Disconnected");
        private readonly PluginText _gamingStatus = new("gaming-status", "Gaming Status", "Idle (No Game Detected)");

        private DynamicMetricPair? _fpsMetric;
        private DynamicMetricPair? _frametimeMetric;

        public MahmPlugin() : this(new MahmReaderService())
        {
        }

        public MahmPlugin(IMahmReaderService readerService) 
            : base("msi-afterburner-plugin", "MSI Afterburner", "Hardware monitoring via MSI Afterburner shared memory")
        {
            _readerService = readerService;
        }

        [Obsolete]
        public override string? ConfigFilePath => null;
        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

        public override void Initialize()
        {
            _readerService.Connect();
        }

        public override void Load(List<IPluginContainer> containers)
        {
            lock (_lock)
            {
                _metrics.Clear();
                _containers.Clear();

                // 案1: スッキリ階層型コンテナ（プレフィックス重複なし）
                var gpuMainContainer = new PluginContainer("gpu-metrics", "GPU");
                var gpuAdvancedContainer = new PluginContainer("gpu-advanced-metrics", "GPU - Advanced & Limits");
                var cpuMainContainer = new PluginContainer("cpu-metrics", "CPU");
                var cpuCoresContainer = new PluginContainer("cpu-cores-metrics", "CPU - Cores");
                var memoryContainer = new PluginContainer("memory-metrics", "Memory");
                var gamingContainer = new PluginContainer("gaming-metrics", "Gaming (RTSS)");
                var otherContainer = new PluginContainer("other-metrics", "Other");

                // ステータス項目の登録
                gpuMainContainer.Entries.Add(_gpuStatus);
                cpuMainContainer.Entries.Add(_cpuStatus);
                gamingContainer.Entries.Add(_gamingStatus);

                // Gaming (RTSS) メトリクスの予約配置
                _fpsMetric = new DynamicMetricPair("framerate", "Framerate", "Framerate", "FPS", "F0");
                _frametimeMetric = new DynamicMetricPair("frametime", "Frametime", "Frametime", "ms", "F1");
                RegisterPair(gamingContainer, _fpsMetric);
                RegisterPair(gamingContainer, _frametimeMetric);

                // MAHM 共有メモリをスキャン
                Dictionary<string, MahmSensorData> initialSensors;
                try
                {
                    initialSensors = _readerService.ReadAllSensors();
                }
                catch
                {
                    initialSensors = new Dictionary<string, MahmSensorData>();
                }

                foreach (var kvp in initialSensors)
                {
                    string name = kvp.Key;
                    var data = kvp.Value;

                    // 予約配置済みの基本Framerate/Frametimeは重複を避ける
                    if (string.Equals(name, "Framerate", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Frametime", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string id = SanitizeId(name);
                    string displayName = GetFriendlyName(name);
                    string unit = string.IsNullOrEmpty(data.Units) ? "" : data.Units;
                    string format = GuessFormat(name, unit);

                    var pair = new DynamicMetricPair(id, displayName, name, unit, format);

                    // 案1のコンテナ振り分け
                    if (IsGamingMetric(name))
                    {
                        RegisterPair(gamingContainer, pair);
                    }
                    else if (IsCpuCoreMetric(name))
                    {
                        RegisterPair(cpuCoresContainer, pair);
                    }
                    else if (IsCpuOverallMetric(name, data.GpuIndex))
                    {
                        RegisterPair(cpuMainContainer, pair);
                    }
                    else if (IsMemoryMetric(name))
                    {
                        RegisterPair(memoryContainer, pair);
                    }
                    else if (IsGpuAdvancedMetric(name))
                    {
                        RegisterPair(gpuAdvancedContainer, pair);
                    }
                    else if (IsGpuMainMetric(name))
                    {
                        RegisterPair(gpuMainContainer, pair);
                    }
                    else
                    {
                        RegisterPair(otherContainer, pair);
                    }
                }

                // 常時登録コンテナ (ステータス確認用)
                containers.Add(gpuMainContainer);
                containers.Add(cpuMainContainer);
                containers.Add(gamingContainer);

                // センサーが存在する場合のみ動的登録するコンテナ
                AddContainerIfNotEmpty(containers, gpuAdvancedContainer);
                AddContainerIfNotEmpty(containers, cpuCoresContainer);
                AddContainerIfNotEmpty(containers, memoryContainer);
                AddContainerIfNotEmpty(containers, otherContainer);

                _containers.AddRange(containers);
            }
        }

        private void RegisterPair(PluginContainer container, DynamicMetricPair pair)
        {
            _metrics.Add(pair);
            container.Entries.Add(pair.Sensor);
            container.Entries.Add(pair.Text);
        }

        private static void AddContainerIfNotEmpty(List<IPluginContainer> list, PluginContainer container)
        {
            bool hasSensors = false;
            foreach (var entry in container.Entries)
            {
                if (entry is PluginSensor)
                {
                    hasSensors = true;
                    break;
                }
            }

            if (hasSensors)
            {
                list.Add(container);
            }
        }

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                var sensors = _readerService.ReadAllSensors();

                lock (_lock)
                {
                    if (sensors.Count == 0)
                    {
                        _gpuStatus.Value = "Afterburner Not Running";
                        _cpuStatus.Value = "Afterburner Not Running";
                        _gamingStatus.Value = "Afterburner Not Running";

                        foreach (var pair in _metrics)
                        {
                            pair.Update(null);
                        }
                        return Task.CompletedTask;
                    }

                    _gpuStatus.Value = "Connected";
                    _cpuStatus.Value = "Connected";

                    bool hasFps = sensors.TryGetValue("Framerate", out var fps) && 
                                  !SensorFormatter.IsInvalid(fps.Value) && fps.Value >= 1.0f;
                    bool hasFrametime = sensors.TryGetValue("Frametime", out var ft) && 
                                        !SensorFormatter.IsInvalid(ft.Value) && ft.Value > 0.0f;
                    bool isGamingActive = hasFps || hasFrametime;
                    _gamingStatus.Value = isGamingActive ? "Active" : "Idle (No Game Detected)";

                    foreach (var pair in _metrics)
                    {
                        if (sensors.TryGetValue(pair.SourceName, out var data))
                        {
                            // ゲーム未検出時は Gaming メトリクスを N/A (未計測) にフォールバック
                            if (IsGamingMetric(pair.SourceName) && !isGamingActive)
                            {
                                pair.Update(null);
                            }
                            else
                            {
                                pair.Update(data.Value);
                            }
                        }
                        else
                        {
                            pair.Update(null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _gpuStatus.Value = $"Error: {ex.Message}";
                }
            }

            return Task.CompletedTask;
        }

        #region 案1 分類判定ロジック

        private static readonly Regex CpuCoreRegex = new(@"^CPU\d+\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool IsGamingMetric(string name)
        {
            return name.StartsWith("Framerate", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Frametime", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCpuCoreMetric(string name)
        {
            return CpuCoreRegex.IsMatch(name);
        }

        private static bool IsCpuOverallMetric(string name, uint gpuIndex)
        {
            if (gpuIndex == uint.MaxValue && name.StartsWith("CPU", StringComparison.OrdinalIgnoreCase))
                return true;

            return name.Equals("CPU temperature", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("CPU usage", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("CPU clock", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("CPU power", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMemoryMetric(string name)
        {
            return name.StartsWith("RAM", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Commit", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGpuAdvancedMetric(string name)
        {
            if (name.Equals("FB usage", StringComparison.OrdinalIgnoreCase))
                return false;

            return name.IndexOf("limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.StartsWith("FB", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("VID", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("BUS", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGpuMainMetric(string name)
        {
            return name.StartsWith("GPU", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Fan", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("FB usage", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("clock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Memory usage", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetFriendlyName(string name)
        {
            if (string.Equals(name, "FB usage", StringComparison.OrdinalIgnoreCase))
            {
                return "FB usage (VRAM Usage)";
            }
            return name;
        }

        private static string SanitizeId(string name)
        {
            return Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+","-").Trim('-');
        }

        private static string GuessFormat(string name, string unit)
        {
            if (unit == "%" || unit == "RPM" || unit == "MHz" || unit == "MB" || unit == "FPS")
                return "F0";

            if (unit == "°C" || unit == "W" || unit == "ms" || unit == "V")
                return "F1";

            return "F1";
        }

        #endregion

        public override void Update() => throw new NotImplementedException();

        public override void Close()
        {
            _readerService.Dispose();
        }
    }
}
