using System.Collections.Generic;
using jp.kshoji.unity.midi;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Maps incoming MIDI CC / pitch bend to VST3 parameters via <see cref="VstMidiParameterMapping"/>.
    /// Supports MIDI Learn and 14-bit CC (MSB/LSB) pairs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostMidiParameterMapper : MonoBehaviour,
        IMidi1ControlChangeEventHandler,
        IMidi1PitchWheelEventHandler,
        IMidi2ControlChangeEventHandler,
        IMidi2PitchWheelEventHandler
    {
        [SerializeField] private int targetPluginId = -1;
        [SerializeField] private VstMidiParameterMapping mapping;
        [SerializeField] private bool autoRegisterWithMidiManager = true;
        [SerializeField] private bool filterByDeviceId;
        [SerializeField] private List<string> allowedDeviceIds = new List<string>();
        [SerializeField] private bool midiLearn;
        [SerializeField] private bool learnAsFourteenBit;
        [Tooltip("When MIDI Learn captures a message, also write into the Mapping asset (Editor / Play Mode).")]
        [SerializeField] private bool persistLearnToMappingAsset = true;

        private bool registered;
        private uint learnParameterId;
        private bool hasLearnParameter;

        // key: (channel<<8)|msbController → last MSB value (0–127)
        private readonly Dictionary<int, int> fourteenBitMsb = new Dictionary<int, int>();
        // key: (channel<<8)|msbController → last LSB value (0–127)
        private readonly Dictionary<int, int> fourteenBitLsb = new Dictionary<int, int>();

        public int TargetPluginId
        {
            get => targetPluginId;
            set => targetPluginId = value;
        }

        public VstMidiParameterMapping Mapping
        {
            get => mapping;
            set => mapping = value;
        }

        public bool MidiLearn
        {
            get => midiLearn;
            set => midiLearn = value;
        }

        public bool IsRegistered => registered;

        /// <summary>Enables device filtering and allows only the given device ids.</summary>
        public void SetAllowedDeviceIds(params string[] deviceIds)
        {
            filterByDeviceId = true;
            allowedDeviceIds = deviceIds != null
                ? new List<string>(deviceIds)
                : new List<string>();
        }

        /// <summary>Last parameter touched (for MIDI Learn). Call from UI when a slider is moved.</summary>
        public void NotifyParameterTouched(uint parameterId)
        {
            learnParameterId = parameterId;
            hasLearnParameter = true;
        }

        private void OnEnable()
        {
            if (autoRegisterWithMidiManager)
                Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public void Register()
        {
            if (registered) return;
            if (MidiManager.Instance == null)
            {
                Debug.LogWarning("[VstHostMidiParameterMapper] MidiManager.Instance is null; cannot register.");
                return;
            }

            MidiManager.Instance.RegisterEventHandleObject(this);
            registered = true;
        }

        public void Unregister()
        {
            if (!registered) return;
            if (MidiManager.Instance != null)
                MidiManager.Instance.UnregisterEventHandleObject(this);
            registered = false;
        }

        private bool AcceptDevice(string deviceId)
        {
            if (targetPluginId < 1)
                return false;
            if (!filterByDeviceId || allowedDeviceIds == null || allowedDeviceIds.Count == 0)
                return true;
            return allowedDeviceIds.Contains(deviceId);
        }

        public void OnMidi1ControlChange(string deviceId, int group, int channel, int function, int value)
        {
            HandleControlChange(deviceId, channel, function, value);
        }

        public void OnMidi2ControlChange(string deviceId, int group, int channel, int index, uint value)
        {
            HandleControlChange(deviceId, channel, index, Midi1Util.DownconvertU32To7(value));
        }

        public void OnMidi1PitchWheel(string deviceId, int group, int channel, int amount)
        {
            HandlePitchBend(deviceId, channel, amount);
        }

        public void OnMidi2PitchWheel(string deviceId, int group, int channel, uint amount)
        {
            HandlePitchBend(deviceId, channel, Midi1Util.DownconvertPitchBend32(amount));
        }

        private void HandleControlChange(string deviceId, int channel, int controller, int value)
        {
            if (!AcceptDevice(deviceId))
                return;

            controller = Mathf.Clamp(controller, 0, 127);
            value = Mathf.Clamp(value, 0, 127);

            if (midiLearn && hasLearnParameter)
            {
                ApplyLearnControlChange(channel, controller);
                return;
            }

            if (mapping == null || mapping.Bindings == null)
                return;

            foreach (var binding in mapping.Bindings)
            {
                if (!ChannelMatches(binding.channel, channel))
                    continue;

                if (binding.source == VstMidiParameterMapping.SourceType.ControlChange)
                {
                    if (binding.controller != controller)
                        continue;
                    ApplyNormalized(binding, value / 127f);
                    continue;
                }

                if (binding.source != VstMidiParameterMapping.SourceType.FourteenBitControlChange)
                    continue;

                var msb = binding.controller;
                var lsb = binding.controllerLsb > 0 ? binding.controllerLsb : (msb <= 31 ? msb + 32 : msb);
                var key = (channel << 8) | msb;

                if (controller == msb)
                {
                    fourteenBitMsb[key] = value;
                    if (fourteenBitLsb.TryGetValue(key, out var lsbValue))
                        ApplyNormalized(binding, ((value << 7) | lsbValue) / 16383f);
                    else
                        ApplyNormalized(binding, (value << 7) / 16383f);
                }
                else if (controller == lsb)
                {
                    fourteenBitLsb[key] = value;
                    if (fourteenBitMsb.TryGetValue(key, out var msbValue))
                        ApplyNormalized(binding, ((msbValue << 7) | value) / 16383f);
                }
            }
        }

        private void HandlePitchBend(string deviceId, int channel, int amount14)
        {
            if (!AcceptDevice(deviceId))
                return;

            amount14 = Mathf.Clamp(amount14, 0, 16383);

            if (midiLearn && hasLearnParameter)
            {
                ApplyLearnPitchBend(channel);
                return;
            }

            if (mapping == null || mapping.Bindings == null)
                return;

            foreach (var binding in mapping.Bindings)
            {
                if (binding.source != VstMidiParameterMapping.SourceType.PitchBend)
                    continue;
                if (!ChannelMatches(binding.channel, channel))
                    continue;
                ApplyNormalized(binding, amount14 / 16383f);
            }
        }

        private void ApplyLearnControlChange(int channel, int controller)
        {
            if (mapping == null)
            {
                Debug.LogWarning("[VstHostMidiParameterMapper] MIDI Learn needs a Mapping asset assigned.", this);
                return;
            }

            mapping.UpsertControlChangeBinding(channel, controller, learnParameterId, learnAsFourteenBit);
            midiLearn = false;
            MarkMappingDirty();
            Debug.Log(
                $"[VstHostMidiParameterMapper] Learned CC {controller} (ch={channel}) → param {learnParameterId}",
                this);
        }

        private void ApplyLearnPitchBend(int channel)
        {
            if (mapping == null)
            {
                Debug.LogWarning("[VstHostMidiParameterMapper] MIDI Learn needs a Mapping asset assigned.", this);
                return;
            }

            mapping.UpsertPitchBendBinding(channel, learnParameterId);
            midiLearn = false;
            MarkMappingDirty();
            Debug.Log(
                $"[VstHostMidiParameterMapper] Learned Pitch Bend (ch={channel}) → param {learnParameterId}",
                this);
        }

        private void ApplyNormalized(VstMidiParameterMapping.Binding binding, float t01)
        {
            var value = binding.MapNormalized(t01);
            VstHostManager.Instance.SetParameterNormalized(targetPluginId, binding.parameterId, value);
        }

        private static bool ChannelMatches(int bindingChannel, int channel) =>
            bindingChannel < 0 || bindingChannel == channel;

        private void MarkMappingDirty()
        {
            if (!persistLearnToMappingAsset || mapping == null)
                return;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mapping);
#endif
        }
    }
}
