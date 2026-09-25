using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace InfoPanel.MAHM.Services
{
    public interface IAfterburnerConfigService
    {
        TimeSpan GetPollingInterval();
    }

    public class AfterburnerConfigService : IAfterburnerConfigService
    {
        private const int DefaultPollingPeriodMs = 1000;
        private const int MinPollingPeriodMs = 100;
        private const int MaxPollingPeriodMs = 10000;

        private const string RegistryKeyPath = @"SOFTWARE\WOW6432Node\MSI\Afterburner";
        private const string RegistryValueName = "InstallDir";

        private static readonly Regex HwPollPeriodRegex = new(@"^\s*HwPollPeriod\s*=\s*(\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public TimeSpan GetPollingInterval()
        {
            try
            {
                string? cfgPath = LocateConfigFile();
                if (!string.IsNullOrEmpty(cfgPath) && File.Exists(cfgPath))
                {
                    string content = File.ReadAllText(cfgPath);
                    int periodMs = ParsePollingPeriod(content);
                    return TimeSpan.FromMilliseconds(periodMs);
                }
            }
            catch
            {
                // エラー時は安全にデフォルト値を返却
            }

            return TimeSpan.FromMilliseconds(DefaultPollingPeriodMs);
        }

        public string? LocateConfigFile()
        {
            try
            {
                // 1. レジストリから取得
                using var key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath);
                if (key != null)
                {
                    object? installDirObj = key.GetValue(RegistryValueName);
                    if (installDirObj is string installDir && !string.IsNullOrWhiteSpace(installDir))
                    {
                        string candidate = Path.Combine(installDir, "Profiles", "MSIAfterburner.cfg");
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }
            catch
            {
                // レジストリアクセスエラー時はフォールバックへ
            }

            // 2. 標準インストール先へフォールバック
            try
            {
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string candidate = Path.Combine(programFilesX86, "MSI Afterburner", "Profiles", "MSIAfterburner.cfg");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // フォールバック失敗
            }

            return null;
        }

        public static int ParsePollingPeriod(string configContent)
        {
            if (string.IsNullOrWhiteSpace(configContent))
            {
                return DefaultPollingPeriodMs;
            }

            var match = HwPollPeriodRegex.Match(configContent);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
            {
                // 安全ガード: 100ms 〜 10000ms の範囲内か
                if (parsed >= MinPollingPeriodMs && parsed <= MaxPollingPeriodMs)
                {
                    return parsed;
                }
            }

            return DefaultPollingPeriodMs;
        }
    }
}
