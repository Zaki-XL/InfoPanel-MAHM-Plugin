using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace InfoPanel.MAHM.Services
{
    /// <summary>
    /// 未測定時の数値センサー（PluginSensor）の出力形式
    /// 0 = 0
    /// 1 = -1
    /// 2 = NaN (デフォルト)
    /// </summary>
    public enum UnmeasuredValueMode
    {
        Zero = 0,     // 0 を出力
        MinusOne = 1, // -1 を出力
        NaN = 2       // float.NaN を出力 (デフォルト)
    }

    /// <summary>
    /// プラグイン設定の読み込みインターフェース
    /// </summary>
    public interface IPluginSettings
    {
        string NaText { get; }
        UnmeasuredValueMode UnmeasuredValueMode { get; }
        float UnmeasuredSensorValue { get; }
    }

    /// <summary>
    /// PluginInfo.ini からプラグイン設定を読み込むクラス
    /// </summary>
    public class PluginSettings : IPluginSettings
    {
        public const string DefaultNaText = "N/A";
        private const string IniFileName = "PluginInfo.ini";

        private static readonly Regex NaTextRegex = new(
            @"^\s*(?:NaText|NanText)\s*=\s*(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex UnmeasuredValueRegex = new(
            @"^\s*UnmeasuredValue\s*=\s*(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string NaText { get; }
        public UnmeasuredValueMode UnmeasuredValueMode { get; }

        public float UnmeasuredSensorValue => UnmeasuredValueMode switch
        {
            UnmeasuredValueMode.Zero => 0.0f,
            UnmeasuredValueMode.MinusOne => -1.0f,
            _ => float.NaN
        };

        public PluginSettings() : this(LoadFromIni())
        {
        }

        public PluginSettings(string? naText, UnmeasuredValueMode mode = UnmeasuredValueMode.NaN)
        {
            NaText = naText ?? DefaultNaText;
            UnmeasuredValueMode = mode;
        }

        private PluginSettings((string naText, UnmeasuredValueMode mode) parsed)
            : this(parsed.naText, parsed.mode)
        {
        }

        /// <summary>
        /// PluginInfo.ini から設定（NaText, UnmeasuredValue）を読み込む
        /// </summary>
        public static (string naText, UnmeasuredValueMode mode) LoadFromIni()
        {
            try
            {
                string? iniPath = LocateIniFile();
                if (!string.IsNullOrEmpty(iniPath) && File.Exists(iniPath))
                {
                    string content = File.ReadAllText(iniPath);
                    return (ParseNaText(content), ParseUnmeasuredValue(content));
                }
            }
            catch
            {
                // ファイル読み込み失敗時は安全にデフォルト値を返却
            }

            return (DefaultNaText, UnmeasuredValueMode.NaN);
        }

        /// <summary>
        /// プラグイン DLL と同階層またはベースディレクトリの PluginInfo.ini を探索する
        /// </summary>
        public static string? LocateIniFile()
        {
            try
            {
                // 1. プラグイン DLL の配置フォルダから探索
                string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    string candidate = Path.Combine(assemblyDir, IniFileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }

            try
            {
                // 2. アプリケーションベースディレクトリから探索
                string baseDir = AppContext.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir))
                {
                    string candidate = Path.Combine(baseDir, IniFileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// ini ファイルの内容から NaText の値を解析する
        /// </summary>
        public static string ParseNaText(string configContent)
        {
            if (string.IsNullOrWhiteSpace(configContent))
            {
                return DefaultNaText;
            }

            using var reader = new StringReader(configContent);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(";") || trimmed.StartsWith("#") || string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var match = NaTextRegex.Match(line);
                if (match.Success)
                {
                    string rawVal = match.Groups[1].Value.Trim();
                    return StripQuotes(rawVal);
                }
            }

            return DefaultNaText;
        }

        /// <summary>
        /// ini ファイルの内容から UnmeasuredValue の値を解析する
        /// 0 = 0, 1 = -1, 2 = NaN (デフォルト)
        /// </summary>
        public static UnmeasuredValueMode ParseUnmeasuredValue(string configContent)
        {
            if (string.IsNullOrWhiteSpace(configContent))
            {
                return UnmeasuredValueMode.NaN;
            }

            using var reader = new StringReader(configContent);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(";") || trimmed.StartsWith("#") || string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var match = UnmeasuredValueRegex.Match(line);
                if (match.Success)
                {
                    string rawVal = StripQuotes(match.Groups[1].Value.Trim());
                    if (int.TryParse(rawVal, out int code))
                    {
                        return code switch
                        {
                            0 => UnmeasuredValueMode.Zero,
                            1 => UnmeasuredValueMode.MinusOne,
                            _ => UnmeasuredValueMode.NaN
                        };
                    }
                }
            }

            return UnmeasuredValueMode.NaN;
        }

        /// <summary>
        /// ダブルクォーテーション "" で囲まれた文字列からクォートを除去して抽出する。
        /// クォート内の空白文字（" - " など）や空文字（""）を保持する。
        /// </summary>
        public static string StripQuotes(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            // 半角ダブルクォーテーション "..."
            if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
            {
                return value.Substring(1, value.Length - 2);
            }

            // 全角引用符 ”...” または “...”
            if (value.Length >= 2 &&
                (value.StartsWith("“") || value.StartsWith("”")) &&
                (value.EndsWith("”") || value.EndsWith("“")))
            {
                return value.Substring(1, value.Length - 2);
            }

            // クォートなしの場合は末尾のインラインコメントを除外
            int commentIndex = value.IndexOf(';');
            if (commentIndex >= 0)
            {
                value = value.Substring(0, commentIndex).Trim();
            }

            return value;
        }
    }
}
