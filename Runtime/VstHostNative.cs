using System;
using System.Runtime.InteropServices;
using AOT;

namespace jp.kshoji.unity.vst3nativehost
{
    public enum VstHostResult : int
    {
        Ok = 0,
        ErrorNotInitialized = -1,
        ErrorAlreadyInitialized = -2,
        ErrorInvalidId = -3,
        ErrorLoadFailed = -4,
        ErrorScanFailed = -5,
        ErrorProcessFailed = -6,
        ErrorInvalidArgument = -7,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VstPluginInfo
    {
        public IntPtr uid;       // char16_t* (UTF-16)
        public IntPtr name;
        public IntPtr vendor;
        public IntPtr category;
        public IntPtr filePath;

        public string Uid => Marshal.PtrToStringUni(uid) ?? string.Empty;
        public string Name => Marshal.PtrToStringUni(name) ?? string.Empty;
        public string Vendor => Marshal.PtrToStringUni(vendor) ?? string.Empty;
        public string Category => Marshal.PtrToStringUni(category) ?? string.Empty;
        public string FilePath => Marshal.PtrToStringUni(filePath) ?? string.Empty;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ScanCallback(IntPtr infoPtr, IntPtr userData);

    public static class VstHostNative
    {
        private const string DllName = "VstHostNative";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_Initialize(int sampleRate, int blockSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_Terminate();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern VstHostResult VstHost_ScanFolder(
            string folderPath,
            ScanCallback callback,
            IntPtr userData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern VstHostResult VstHost_Load(
            string filePath,
            string uid,
            out int outId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_Unload(int id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_SendMidi1(
            int id,
            byte status,
            byte data1,
            byte data2);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe VstHostResult VstHost_Process(
            int id,
            float* inputL,
            float* inputR,
            float* outputL,
            float* outputR,
            int numFrames);
    }
}
