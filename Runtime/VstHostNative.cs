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
        ErrorNotSupported = -8,
        ErrorBufferTooSmall = -9,
        /// Unload/Terminate timed out while a Process (or borrower) still holds the instance.
        ErrorBusy = -10,
    }

    [Flags]
    public enum VstParamFlags : int
    {
        None = 0,
        CanAutomate = 1 << 0,
        IsReadOnly = 1 << 1,
        IsWrapAround = 1 << 2,
        IsList = 1 << 3,
        IsHidden = 1 << 4,
        IsProgramChange = 1 << 15,
        IsBypass = 1 << 16,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VstPluginInfo
    {
        public IntPtr uid;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct VstParamInfo
    {
        public uint Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Title;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ShortTitle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Units;
        public int StepCount;
        public double DefaultNormalized;
        public int Flags;

        public VstParamFlags ParamFlags => (VstParamFlags)Flags;
        public bool IsReadOnly => (ParamFlags & VstParamFlags.IsReadOnly) != 0
                                  || (ParamFlags & VstParamFlags.IsHidden) != 0;
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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_GetParameterCount(int id, out int outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_GetParameterInfo(int id, int index, out VstParamInfo outInfo);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_GetParameterNormalized(int id, uint paramId, out double outValue);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_SetParameterNormalized(int id, uint paramId, double value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_GetProgramCount(int id, out int outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_GetProgramName(
            int id,
            int index,
            IntPtr outName,
            int nameChars);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_SetProgram(int id, int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_GetState(
            int id,
            byte[] buffer,
            int bufferSize,
            out int outWritten);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern VstHostResult VstHost_SetState(int id, byte[] buffer, int size);
    }
}
