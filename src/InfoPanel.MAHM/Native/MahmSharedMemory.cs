using System;
using System.Runtime.InteropServices;

namespace InfoPanel.MAHM.Native
{
    /// <summary>
    /// MSI Afterburner 共有メモリのヘッダー構造体
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct MAHM_SHARED_MEMORY_HEADER
    {
        public uint dwSignature;    // 'MAHM' (0x4D41484D)
        public uint dwVersion;      // 0x00020000 など
        public uint dwHeaderSize;   // sizeof(MAHM_SHARED_MEMORY_HEADER)
        public uint dwNumEntries;   // 登録されているエントリ数
        public uint dwEntrySize;    // sizeof(MAHM_SHARED_MEMORY_ENTRY)
        public uint time;           // 最終更新タイムスタンプ (time_t)
        public uint dwNumGpuEntries;// GPUエントリ数
        public uint dwGpuEntrySize; // GPUエントリサイズ
    }

    /// <summary>
    /// MSI Afterburner 共有メモリの各センサーエントリ構造体
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
    public struct MAHM_SHARED_MEMORY_ENTRY
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szSrcName;             // センサー名 (例: "GPU usage", "Memory usage")

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szSrcUnits;            // 単位 (例: "%", "MB", "°C", "RPM")

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szLocalizedSrcName;    // ローカライズ名

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szLocalizedSrcUnits;   // ローカライズ単位

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szRecommendedFormat;   // 推奨フォーマット文字列 (例: "%.0f")

        public float data;                   // 現在のセンサー値
        public float minLimit;               // 最小許容値
        public float maxLimit;               // 最大許容値
        public uint dwFlags;                 // フラグ
        public uint dwGpu;                   // 関連付けられているGPUインデックス (0始まり)
    }

    /// <summary>
    /// Win32 API 宣言
    /// </summary>
    internal static class NativeMethods
    {
        public const uint FILE_MAP_READ = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenFileMapping(
            uint dwDesiredAccess,
            bool bInheritHandle,
            string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr MapViewOfFile(
            IntPtr hFileMappingObject,
            uint dwDesiredAccess,
            uint dwFileOffsetHigh,
            uint dwFileOffsetLow,
            UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}
