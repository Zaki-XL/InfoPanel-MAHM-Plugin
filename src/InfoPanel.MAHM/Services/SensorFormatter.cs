using System;

namespace InfoPanel.MAHM.Services
{
    /// <summary>
    /// センサー値の整形および N/A 表示を行うヘルパークラス
    /// </summary>
    public static class SensorFormatter
    {
        public const string NaText = "N/A";
        public const float SentinelThreshold = 3.4e38f; // MSI Afterburner / RTSS FLT_MAX センチネル

        /// <summary>
        /// 値が無効値（未計測・無効センチネル・非数・無限大）であるか判定する
        /// </summary>
        public static bool IsInvalid(float? value)
        {
            if (!value.HasValue) return true;
            float v = value.Value;
            return float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) >= SentinelThreshold;
        }

        /// <summary>
        /// センサー値を単位付き文字列に整形する。
        /// 値が無効な場合は "N/A" を返す。
        /// </summary>
        public static string Format(float? value, string unit, string format = "F1")
        {
            if (IsInvalid(value))
            {
                return NaText;
            }

            string formattedNumber = value!.Value.ToString(format);
            return string.IsNullOrEmpty(unit) ? formattedNumber : $"{formattedNumber} {unit}";
        }

        /// <summary>
        /// 整数表示用のフォーマット（ファン回転数、クロック、FPSなど）
        /// </summary>
        public static string FormatInt(float? value, string unit)
        {
            return Format(value, unit, "F0");
        }
    }
}
