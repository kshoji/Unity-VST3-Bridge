using System.Collections.Generic;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Runtime / Play Mode IMGUI browser for host programs and <see cref="VstPresetAsset"/> slots.
    /// Includes A/B state comparison.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstPresetBrowser : MonoBehaviour
    {
        [SerializeField] private int pluginId = -1;
        [SerializeField] private List<VstPresetAsset> presets = new List<VstPresetAsset>();
        [SerializeField] private bool showPrograms = true;
        [SerializeField] private bool showGui = true;
        [SerializeField] private Rect windowRect = new Rect(400, 20, 360, 420);

        private readonly List<string> programs = new List<string>();
        private int programIndex;
        private Vector2 scroll;
        private byte[] slotA;
        private byte[] slotB;
        private bool preferSlotB;
        private string status = string.Empty;

        public int PluginId
        {
            get => pluginId;
            set
            {
                if (pluginId == value) return;
                pluginId = value;
                RefreshPrograms();
            }
        }

        public IList<VstPresetAsset> Presets => presets;

        private void OnEnable()
        {
            if (pluginId >= 1)
                RefreshPrograms();
        }

        public void RefreshPrograms()
        {
            programs.Clear();
            if (pluginId < 1 || !VstHostManager.Instance.IsInitialized)
                return;

            programs.AddRange(VstHostManager.Instance.GetPrograms(pluginId));
            programIndex = Mathf.Clamp(programIndex, 0, Mathf.Max(0, programs.Count - 1));
        }

        public bool ApplyPreset(VstPresetAsset preset)
        {
            if (preset == null || pluginId < 1)
                return false;
            if (!preset.ApplyTo(pluginId))
            {
                status = "Apply failed";
                return false;
            }

            status = $"Applied '{preset.DisplayName}' ({preset.State.Length} bytes)";
            RefreshPrograms();
            return true;
        }

        public bool CaptureToSlotA()
        {
            slotA = CaptureState();
            status = slotA == null ? "Capture A failed" : $"Slot A saved ({slotA.Length} bytes)";
            return slotA != null;
        }

        public bool CaptureToSlotB()
        {
            slotB = CaptureState();
            status = slotB == null ? "Capture B failed" : $"Slot B saved ({slotB.Length} bytes)";
            return slotB != null;
        }

        public bool ApplySlotA()
        {
            if (slotA == null || slotA.Length == 0 || pluginId < 1)
                return false;
            var ok = VstHostManager.Instance.SetState(pluginId, slotA);
            if (ok)
            {
                preferSlotB = false;
                status = "Restored slot A";
                RefreshPrograms();
            }
            return ok;
        }

        public bool ApplySlotB()
        {
            if (slotB == null || slotB.Length == 0 || pluginId < 1)
                return false;
            var ok = VstHostManager.Instance.SetState(pluginId, slotB);
            if (ok)
            {
                preferSlotB = true;
                status = "Restored slot B";
                RefreshPrograms();
            }
            return ok;
        }

        /// <summary>Toggles between A and B when both slots have state.</summary>
        public bool ToggleAb()
        {
            if (preferSlotB)
                return ApplySlotA();
            return ApplySlotB();
        }

        private byte[] CaptureState()
        {
            if (pluginId < 1 || !VstHostManager.Instance.IsInitialized)
                return null;
            return VstHostManager.Instance.GetState(pluginId);
        }

        private void OnGUI()
        {
            if (!showGui || pluginId < 1)
                return;

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "VST Preset Browser");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label($"Plugin id={pluginId}");
            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status);

            if (GUILayout.Button("Refresh Programs"))
                RefreshPrograms();

            if (showPrograms && programs.Count > 0)
            {
                GUILayout.Label($"Host programs ({programs.Count})");
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(140));
                var next = GUILayout.SelectionGrid(programIndex, programs.ToArray(), 1);
                if (next != programIndex)
                {
                    programIndex = next;
                    VstHostManager.Instance.SetProgram(pluginId, programIndex);
                    status = $"Program {programIndex}: {programs[programIndex]}";
                }
                GUILayout.EndScrollView();
            }
            else if (showPrograms)
            {
                GUILayout.Label("No host program list exposed by this plugin.");
            }

            GUILayout.Space(6);
            GUILayout.Label("Preset assets");
            if (presets != null)
            {
                for (var i = 0; i < presets.Count; i++)
                {
                    var preset = presets[i];
                    if (preset == null)
                        continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(preset.DisplayName, GUILayout.ExpandWidth(true));
                    GUI.enabled = preset.HasState;
                    if (GUILayout.Button("Apply", GUILayout.Width(60)))
                        ApplyPreset(preset);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("A/B compare");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture A"))
                CaptureToSlotA();
            GUI.enabled = slotA != null && slotA.Length > 0;
            if (GUILayout.Button("Restore A"))
                ApplySlotA();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture B"))
                CaptureToSlotB();
            GUI.enabled = slotB != null && slotB.Length > 0;
            if (GUILayout.Button("Restore B"))
                ApplySlotB();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUI.enabled = slotA != null && slotA.Length > 0 && slotB != null && slotB.Length > 0;
            if (GUILayout.Button($"Toggle A/B (current {(preferSlotB ? "B" : "A")})"))
                ToggleAb();
            GUI.enabled = true;

            GUI.DragWindow();
        }
    }
}
