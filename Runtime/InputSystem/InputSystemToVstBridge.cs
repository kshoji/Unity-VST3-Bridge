#if FEATURE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jp.kshoji.unity.vst3nativehost.inputsystem
{
    public enum InputToVstMessageType
    {
        NoteOnOff = 0,
        ParameterNormalized = 1,
        ControlChange = 2,
        PitchBend = 3,
        ProgramChange = 4,
    }

    [Serializable]
    public sealed class InputToVstBinding
    {
        public string actionName;
        public InputToVstMessageType messageType = InputToVstMessageType.NoteOnOff;

        [Range(0, 15)]
        public int channel;

        [Tooltip("Note / CC number / program, depending on message type.")]
        public int number = 60;

        [Tooltip("Fixed velocity when not using action value (Note On).")]
        [Range(0, 127)]
        public int velocity = 100;

        [Tooltip("VST parameter id when messageType is ParameterNormalized.")]
        public uint parameterId;

        [Tooltip("When true, read action float (0–1) for velocity / CC / parameter / bend.")]
        public bool useActionValue = true;

        public bool sendOnPerformed = true;
        public bool sendOnCanceled = true;
    }

    /// <summary>
    /// Maps Input System actions to VST notes / parameters directly (no MIDI Plugin required).
    /// For MIDI-routed workflows, prefer InputSystemToMidiBridge → virtual device → VstHostMidiAdapter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputSystemToVstBridge : MonoBehaviour
    {
        [SerializeField] private VstParameterTarget target;
        [SerializeField] private InputActionAsset actionAsset;
        [SerializeField] private InputToVstBinding[] bindings = Array.Empty<InputToVstBinding>();

        private readonly List<Action<InputAction.CallbackContext>> performedHandlers =
            new List<Action<InputAction.CallbackContext>>();
        private readonly List<Action<InputAction.CallbackContext>> canceledHandlers =
            new List<Action<InputAction.CallbackContext>>();

        public VstParameterTarget Target
        {
            get => target;
            set => target = value;
        }

        public InputActionAsset ActionAsset
        {
            get => actionAsset;
            set => actionAsset = value;
        }

        private void OnEnable()
        {
            if (actionAsset == null || bindings == null)
                return;

            performedHandlers.Clear();
            canceledHandlers.Clear();
            actionAsset.Enable();

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null || string.IsNullOrEmpty(binding.actionName))
                    continue;

                var action = actionAsset.FindAction(binding.actionName);
                if (action == null)
                    continue;

                Action<InputAction.CallbackContext> performed = ctx => Handle(binding, ctx, true);
                Action<InputAction.CallbackContext> canceled = ctx => Handle(binding, ctx, false);
                performedHandlers.Add(performed);
                canceledHandlers.Add(canceled);
                action.performed += performed;
                action.canceled += canceled;
            }
        }

        private void OnDisable()
        {
            if (actionAsset == null || bindings == null)
                return;

            var handlerIndex = 0;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null || string.IsNullOrEmpty(binding.actionName))
                    continue;

                var action = actionAsset.FindAction(binding.actionName);
                if (action == null)
                    continue;

                if (handlerIndex < performedHandlers.Count)
                {
                    action.performed -= performedHandlers[handlerIndex];
                    action.canceled -= canceledHandlers[handlerIndex];
                    handlerIndex++;
                }
            }

            actionAsset.Disable();
        }

        private void Handle(InputToVstBinding binding, InputAction.CallbackContext context, bool performed)
        {
            if (performed ? !binding.sendOnPerformed : !binding.sendOnCanceled)
                return;

            var pluginId = target != null ? target.PluginId : -1;
            if (pluginId < 1 || !VstHostManager.Instance.IsInitialized)
                return;

            var host = VstHostManager.Instance;
            var actionValue = binding.useActionValue ? Mathf.Clamp01(context.ReadValue<float>()) : 1f;

            switch (binding.messageType)
            {
                case InputToVstMessageType.NoteOnOff:
                    if (performed)
                    {
                        var velocity = binding.useActionValue
                            ? Mathf.Clamp(Mathf.RoundToInt(actionValue * 127f), 1, 127)
                            : binding.velocity;
                        host.NoteOn(pluginId, binding.channel, binding.number, velocity);
                    }
                    else
                    {
                        host.NoteOff(pluginId, binding.channel, binding.number, 0);
                    }
                    break;

                case InputToVstMessageType.ParameterNormalized:
                    if (performed || binding.useActionValue)
                        host.SetParameterNormalized(pluginId, binding.parameterId, actionValue);
                    break;

                case InputToVstMessageType.ControlChange:
                    if (performed || binding.useActionValue)
                    {
                        var cc = binding.useActionValue
                            ? Mathf.Clamp(Mathf.RoundToInt(actionValue * 127f), 0, 127)
                            : binding.velocity;
                        host.ControlChange(pluginId, binding.channel, binding.number, cc);
                    }
                    break;

                case InputToVstMessageType.PitchBend:
                    if (performed || binding.useActionValue)
                    {
                        var amount = binding.useActionValue
                            ? Mathf.Clamp(Mathf.RoundToInt(actionValue * 16383f), 0, 16383)
                            : 8192;
                        host.PitchBend(pluginId, binding.channel, amount);
                    }
                    break;

                case InputToVstMessageType.ProgramChange:
                    if (performed)
                    {
                        var program = binding.useActionValue
                            ? Mathf.Clamp(Mathf.RoundToInt(actionValue * 127f), 0, 127)
                            : binding.number;
                        host.SetProgram(pluginId, program);
                        host.ProgramChange(pluginId, binding.channel, program);
                    }
                    break;
            }
        }
    }
}
#endif
