using System.Diagnostics;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Lifecycle / load tracing. Compiled away unless scripting define
    /// <c>VSTHOST_DEBUG</c> is set (Project Settings → Player → Scripting Define Symbols).
    /// Errors and warnings always use <see cref="Debug"/> directly.
    /// </summary>
    internal static class VstHostLog
    {
        [Conditional("VSTHOST_DEBUG")]
        public static void Trace(string message) => Debug.Log(message);

        [Conditional("VSTHOST_DEBUG")]
        public static void TraceFormat(string format, params object[] args) =>
            Debug.LogFormat(format, args);
    }
}
