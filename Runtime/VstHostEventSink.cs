using UnityEngine;
using UnityEngine.Events;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// UnityEvent / Inspector-friendly entry points for VST host control.
    /// Works standalone (no MIDI package). Wire from <c>MidiInputRouter</c> bindings when MIDI is present.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostEventSink : MonoBehaviour
    {
        [SerializeField] private VstParameterTarget target;
        [SerializeField] private int pluginId = -1;
        [SerializeField] private int channel;
        [SerializeField] private int defaultVelocity = 100;

        [Header("Optional callbacks")]
        public UnityEvent onNoteOn;
        public UnityEvent onNoteOff;

        public int PluginId
        {
            get
            {
                if (target != null && target.PluginId >= 1)
                    return target.PluginId;
                return pluginId;
            }
            set => pluginId = value;
        }

        private void Reset()
        {
            target = GetComponent<VstParameterTarget>();
        }

        public void NoteOn(int note) => NoteOn(note, defaultVelocity);

        public void NoteOn(int note, int velocity)
        {
            var id = PluginId;
            if (id < 1 || !VstHostManager.Instance.IsInitialized)
                return;
            VstHostManager.Instance.NoteOn(id, channel, note, velocity);
            onNoteOn?.Invoke();
        }

        public void NoteOff(int note)
        {
            var id = PluginId;
            if (id < 1 || !VstHostManager.Instance.IsInitialized)
                return;
            VstHostManager.Instance.NoteOff(id, channel, note, 0);
            onNoteOff?.Invoke();
        }

        public void SetParameter(uint parameterId, float normalized)
        {
            var id = PluginId;
            if (id < 1 || !VstHostManager.Instance.IsInitialized)
                return;
            VstHostManager.Instance.SetParameterNormalized(id, parameterId, Mathf.Clamp01(normalized));
        }

        public void SetProgram(int programIndex)
        {
            var id = PluginId;
            if (id < 1 || !VstHostManager.Instance.IsInitialized)
                return;
            VstHostManager.Instance.SetProgram(id, programIndex);
        }

        public void ControlChange(int controller, int value)
        {
            var id = PluginId;
            if (id < 1 || !VstHostManager.Instance.IsInitialized)
                return;
            VstHostManager.Instance.ControlChange(id, channel, controller, value);
        }

        public void PitchBend(int value14)
        {
            var id = PluginId;
            if (id < 1 || !VstHostManager.Instance.IsInitialized)
                return;
            VstHostManager.Instance.PitchBend(id, channel, value14);
        }
    }
}
