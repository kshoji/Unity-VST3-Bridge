using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// MIDI 1.0 short-message helpers and MIDI 2.0 channel-voice down-conversion.
    /// SysEx / per-note / high-res controllers are skipped (logged once per type).
    /// </summary>
    public static class Midi1Util
    {
        private static bool loggedSkippedSysex;
        private static bool loggedSkippedPerNote;
        private static bool loggedSkippedUnknown;

        public static byte Status(int statusHi, int channel) =>
            (byte)((statusHi & 0xF0) | (channel & 0x0F));

        public static void NoteOn(int channel, int note, int velocity, out byte status, out byte data1, out byte data2)
        {
            status = Status(0x90, channel);
            data1 = (byte)(note & 0x7F);
            data2 = (byte)(velocity & 0x7F);
        }

        public static void NoteOff(int channel, int note, int velocity, out byte status, out byte data1, out byte data2)
        {
            status = Status(0x80, channel);
            data1 = (byte)(note & 0x7F);
            data2 = (byte)(velocity & 0x7F);
        }

        public static void ControlChange(int channel, int controller, int value, out byte status, out byte data1, out byte data2)
        {
            status = Status(0xB0, channel);
            data1 = (byte)(controller & 0x7F);
            data2 = (byte)(value & 0x7F);
        }

        public static void ProgramChange(int channel, int program, out byte status, out byte data1, out byte data2)
        {
            status = Status(0xC0, channel);
            data1 = (byte)(program & 0x7F);
            data2 = 0;
        }

        public static void ChannelAftertouch(int channel, int pressure, out byte status, out byte data1, out byte data2)
        {
            status = Status(0xD0, channel);
            data1 = (byte)(pressure & 0x7F);
            data2 = 0;
        }

        public static void PitchBend(int channel, int amount14, out byte status, out byte data1, out byte data2)
        {
            var amount = Mathf.Clamp(amount14, 0, 16383);
            status = Status(0xE0, channel);
            data1 = (byte)(amount & 0x7F);
            data2 = (byte)((amount >> 7) & 0x7F);
        }

        public static void PolyphonicAftertouch(int channel, int note, int pressure, out byte status, out byte data1, out byte data2)
        {
            status = Status(0xA0, channel);
            data1 = (byte)(note & 0x7F);
            data2 = (byte)(pressure & 0x7F);
        }

        /// <summary>MIDI 2.0 16-bit velocity → MIDI 1.0 7-bit.</summary>
        public static int DownconvertVelocity16(int velocity16) => (velocity16 >> 9) & 0x7F;

        /// <summary>MIDI 2.0 32-bit controller / pressure → MIDI 1.0 7-bit.</summary>
        public static int DownconvertU32To7(uint value) => (int)((value >> 25) & 0x7F);

        /// <summary>MIDI 2.0 32-bit pitch bend → MIDI 1.0 14-bit.</summary>
        public static int DownconvertPitchBend32(uint amount) => (int)((amount >> 18) & 0x3FFF);

        public static void LogSkippedSysex()
        {
            if (loggedSkippedSysex) return;
            loggedSkippedSysex = true;
            Debug.LogWarning("[VstHost] SysEx / unknown SysEx-like messages are skipped.");
        }

        public static void LogSkippedPerNote()
        {
            if (loggedSkippedPerNote) return;
            loggedSkippedPerNote = true;
            Debug.LogWarning("[VstHost] Per-note / high-resolution MIDI 2.0 controllers are skipped.");
        }

        public static void LogSkippedUnknown()
        {
            if (loggedSkippedUnknown) return;
            loggedSkippedUnknown = true;
            Debug.LogWarning("[VstHost] Unsupported MIDI message skipped.");
        }
    }
}
