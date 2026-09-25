using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using InfoPanel.MAHM.Native;

namespace InfoPanel.MAHM.Services
{
    public static class MahmBufferParser
    {
        public const uint ExpectedSignature = 0x4D41484D; // 'MAHM'
        public const float SentinelThreshold = 3.4e38f; // MSI Afterburner / RTSS FLT_MAX センチネル

        public static bool TryParseHeader(IntPtr pBuffer, out MAHM_SHARED_MEMORY_HEADER header)
        {
            header = default;
            if (pBuffer == IntPtr.Zero) return false;

            try
            {
                header = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_HEADER>(pBuffer);
                return header.dwSignature == ExpectedSignature;
            }
            catch
            {
                return false;
            }
        }

        public static Dictionary<string, MahmSensorData> ParseEntries(IntPtr pBuffer, MAHM_SHARED_MEMORY_HEADER header)
        {
            var result = new Dictionary<string, MahmSensorData>(StringComparer.OrdinalIgnoreCase);
            if (pBuffer == IntPtr.Zero || header.dwNumEntries == 0 || header.dwEntrySize == 0)
            {
                return result;
            }

            IntPtr pEntry = IntPtr.Add(pBuffer, (int)header.dwHeaderSize);
            int entrySize = (int)header.dwEntrySize;

            for (int i = 0; i < header.dwNumEntries; i++)
            {
                try
                {
                    var entry = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_ENTRY>(pEntry);
                    if (!string.IsNullOrEmpty(entry.szSrcName))
                    {
                        if (!result.ContainsKey(entry.szSrcName) || entry.dwGpu == uint.MaxValue)
                        {
                            // FLT_MAX(3.4028235E+38)等のセンチネル値やNaN/Infinityは float.NaN に正規化
                            float cleanVal = entry.data;
                            if (float.IsNaN(cleanVal) || float.IsInfinity(cleanVal) || Math.Abs(cleanVal) >= SentinelThreshold)
                            {
                                cleanVal = float.NaN;
                            }

                            result[entry.szSrcName] = new MahmSensorData
                            {
                                Name = entry.szSrcName,
                                Units = entry.szSrcUnits,
                                Value = cleanVal,
                                GpuIndex = entry.dwGpu
                            };
                        }
                    }
                }
                catch
                {
                    // Skip malformed entry
                }

                pEntry = IntPtr.Add(pEntry, entrySize);
            }

            return result;
        }
    }
}
