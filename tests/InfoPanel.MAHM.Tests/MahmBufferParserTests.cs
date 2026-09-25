using System;
using System.Runtime.InteropServices;
using Xunit;
using InfoPanel.MAHM.Native;
using InfoPanel.MAHM.Services;

namespace InfoPanel.MAHM.Tests
{
    public class MahmBufferParserTests
    {
        [Fact]
        public void TryParseHeader_WhenBufferIsZero_ReturnsFalse()
        {
            bool success = MahmBufferParser.TryParseHeader(IntPtr.Zero, out var header);
            Assert.False(success);
        }

        [Fact]
        public void TryParseHeader_WhenSignatureIsInvalid_ReturnsFalse()
        {
            var header = new MAHM_SHARED_MEMORY_HEADER
            {
                dwSignature = 0x12345678,
                dwHeaderSize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_HEADER>(),
                dwNumEntries = 1
            };

            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(header));
            try
            {
                Marshal.StructureToPtr(header, p, false);
                bool success = MahmBufferParser.TryParseHeader(p, out var parsed);
                Assert.False(success);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        [Fact]
        public void TryParseHeader_WhenSignatureIsValid_ReturnsTrue()
        {
            var header = new MAHM_SHARED_MEMORY_HEADER
            {
                dwSignature = MahmBufferParser.ExpectedSignature,
                dwHeaderSize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_HEADER>(),
                dwNumEntries = 2,
                dwEntrySize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_ENTRY>()
            };

            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(header));
            try
            {
                Marshal.StructureToPtr(header, p, false);
                bool success = MahmBufferParser.TryParseHeader(p, out var parsed);
                Assert.True(success);
                Assert.Equal(2u, parsed.dwNumEntries);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        [Fact]
        public void ParseEntries_WhenDataIsFltMax_NormalizesToNaN()
        {
            // FLT_MAX (センチネル値) が渡された場合、float.NaN に正規化されることを検証
            var header = new MAHM_SHARED_MEMORY_HEADER
            {
                dwSignature = MahmBufferParser.ExpectedSignature,
                dwHeaderSize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_HEADER>(),
                dwNumEntries = 1,
                dwEntrySize = (uint)Marshal.SizeOf<MAHM_SHARED_MEMORY_ENTRY>()
            };

            var entry = new MAHM_SHARED_MEMORY_ENTRY
            {
                szSrcName = "Framerate",
                szSrcUnits = "FPS",
                data = float.MaxValue, // 3.4028235E+38
                dwGpu = uint.MaxValue
            };

            int totalSize = (int)header.dwHeaderSize + (int)header.dwEntrySize;
            IntPtr pBuffer = Marshal.AllocHGlobal(totalSize);
            try
            {
                Marshal.StructureToPtr(header, pBuffer, false);
                IntPtr pEntry = IntPtr.Add(pBuffer, (int)header.dwHeaderSize);
                Marshal.StructureToPtr(entry, pEntry, false);

                var result = MahmBufferParser.ParseEntries(pBuffer, header);
                Assert.True(result.ContainsKey("Framerate"));
                Assert.True(float.IsNaN(result["Framerate"].Value));
            }
            finally
            {
                Marshal.FreeHGlobal(pBuffer);
            }
        }
    }
}
