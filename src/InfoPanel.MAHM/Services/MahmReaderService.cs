using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using InfoPanel.MAHM.Native;

namespace InfoPanel.MAHM.Services
{
    public class MahmSensorData
    {
        public string Name { get; set; } = string.Empty;
        public string Units { get; set; } = string.Empty;
        public float Value { get; set; }
        public uint GpuIndex { get; set; }
    }

    public interface IMahmReaderService : IDisposable
    {
        bool IsConnected { get; }
        bool Connect();
        Dictionary<string, MahmSensorData> ReadAllSensors();
        void Disconnect();
    }

    public class MahmReaderService : IMahmReaderService
    {
        private const string MappingName = "MAHMSharedMemory";

        private IntPtr _hMapFile = IntPtr.Zero;
        private IntPtr _pBuffer = IntPtr.Zero;
        private bool _disposed = false;

        public bool IsConnected => _pBuffer != IntPtr.Zero;

        public bool Connect()
        {
            if (_pBuffer != IntPtr.Zero) return true;

            try
            {
                _hMapFile = NativeMethods.OpenFileMapping(NativeMethods.FILE_MAP_READ, false, MappingName);
                if (_hMapFile == IntPtr.Zero) return false;

                _pBuffer = NativeMethods.MapViewOfFile(_hMapFile, NativeMethods.FILE_MAP_READ, 0, 0, UIntPtr.Zero);
                if (_pBuffer == IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(_hMapFile);
                    _hMapFile = IntPtr.Zero;
                    return false;
                }

                if (!MahmBufferParser.TryParseHeader(_pBuffer, out _))
                {
                    Disconnect();
                    return false;
                }

                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public Dictionary<string, MahmSensorData> ReadAllSensors()
        {
            var result = new Dictionary<string, MahmSensorData>(StringComparer.OrdinalIgnoreCase);

            if (!IsConnected && !Connect())
            {
                return result;
            }

            try
            {
                if (!MahmBufferParser.TryParseHeader(_pBuffer, out var header))
                {
                    Disconnect();
                    return result;
                }

                return MahmBufferParser.ParseEntries(_pBuffer, header);
            }
            catch
            {
                Disconnect();
                return result;
            }
        }

        public void Disconnect()
        {
            if (_pBuffer != IntPtr.Zero)
            {
                NativeMethods.UnmapViewOfFile(_pBuffer);
                _pBuffer = IntPtr.Zero;
            }

            if (_hMapFile != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_hMapFile);
                _hMapFile = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
