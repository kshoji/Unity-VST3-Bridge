using System;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>Kind of host activity for editor / debug monitors.</summary>
    public enum VstHostActivityKind
    {
        Midi1 = 0,
        Parameter = 1,
        Program = 2,
        State = 3,
    }

    /// <summary>One host activity record (MIDI send, parameter change, etc.).</summary>
    public readonly struct VstHostActivityEntry
    {
        public readonly double TimeSeconds;
        public readonly VstHostActivityKind Kind;
        public readonly int PluginId;
        public readonly string Detail;

        public VstHostActivityEntry(double timeSeconds, VstHostActivityKind kind, int pluginId, string detail)
        {
            TimeSeconds = timeSeconds;
            Kind = kind;
            PluginId = pluginId;
            Detail = detail ?? string.Empty;
        }
    }

    /// <summary>
    /// Lightweight activity bus for editor monitors and diagnostics.
    /// Subscribers must not allocate heavily; events may fire at MIDI rate.
    /// </summary>
    public static class VstHostActivity
    {
        public static event Action<VstHostActivityEntry> Raised;

        public static bool Enabled { get; set; } = true;

        public static void Raise(VstHostActivityKind kind, int pluginId, string detail)
        {
            if (!Enabled)
                return;
            var handler = Raised;
            if (handler == null)
                return;
            handler(new VstHostActivityEntry(
                Time.realtimeSinceStartup,
                kind,
                pluginId,
                detail));
        }
    }
}
