using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>Scan / filter / preview VST3 plugins installed on the machine.</summary>
    internal sealed class VstPluginBrowserWindow : EditorWindow
    {
        private readonly List<VstHostManager.ScannedPlugin> scanned = new List<VstHostManager.ScannedPlugin>();
        private Vector2 listScroll;
        private string filter = string.Empty;
        private string categoryFilter = "All";
        private int selectedIndex;
        private int previewPluginId = -1;
        private int previewNote = 60;
        private int previewVelocity = 100;
        private string status = string.Empty;

        [MenuItem("Window/VST3 Host/Plugin Browser")]
        private static void Open()
        {
            var window = GetWindow<VstPluginBrowserWindow>();
            window.titleContent = new GUIContent("VST3 Plugin Browser");
            window.minSize = new Vector2(480, 360);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnloadPreview();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Native host is terminated on exit; drop stale preview id / scene object.
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                previewPluginId = -1;
                VstHostEditorPreview.Clear();
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);
            DrawFilters();
            EditorGUILayout.Space(4);
            DrawList();
            EditorGUILayout.Space(4);
            DrawPreview();
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, MessageType.None);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Scan Default", EditorStyles.toolbarButton, GUILayout.Width(100)))
                Rescan(null);
            if (GUILayout.Button("Scan Extra Folders", EditorStyles.toolbarButton, GUILayout.Width(130)))
                RescanExtra();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Count: {scanned.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilters()
        {
            filter = EditorGUILayout.TextField("Name / Vendor", filter);
            var categories = new List<string> { "All" };
            categories.AddRange(scanned.Select(p => string.IsNullOrEmpty(p.Category) ? "(none)" : p.Category).Distinct().OrderBy(c => c));
            var catIndex = Mathf.Max(0, categories.IndexOf(categoryFilter));
            catIndex = EditorGUILayout.Popup("Category", catIndex, categories.ToArray());
            categoryFilter = categories[catIndex];
        }

        private void DrawList()
        {
            var filtered = GetFiltered().ToList();
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
            for (var i = 0; i < filtered.Count; i++)
            {
                var p = filtered[i];
                var label = $"{p.Name}  —  {p.Vendor}  [{p.Category}]";
                var selected = selectedIndex == i;
                if (GUILayout.Toggle(selected, label, "Button") && !selected)
                    selectedIndex = i;
            }
            EditorGUILayout.EndScrollView();

            if (filtered.Count == 0)
                EditorGUILayout.HelpBox("No plugins. Press Scan Default after the host initializes.", MessageType.Info);
            else if (selectedIndex >= 0 && selectedIndex < filtered.Count)
            {
                var p = filtered[selectedIndex];
                EditorGUILayout.LabelField("Path", p.FilePath);
                EditorGUILayout.LabelField("UID", p.Uid);
            }
        }

        private void DrawPreview()
        {
            var hostReady = VstHostManager.Instance.IsInitialized;
            var hasSelection = scanned.Count > 0 && selectedIndex >= 0;
            var hasPreview = previewPluginId >= 1;

            EditorGUILayout.BeginHorizontal();
            previewNote = EditorGUILayout.IntSlider("Note", previewNote, 0, 127);
            previewVelocity = EditorGUILayout.IntSlider("Velocity", previewVelocity, 1, 127);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Load Selected"))
                    LoadSelected();
            }

            using (new EditorGUI.DisabledScope(!hasPreview))
            {
                if (GUILayout.Button("Note On"))
                    VstHostManager.Instance.NoteOn(previewPluginId, 0, previewNote, previewVelocity);
                if (GUILayout.Button("Note Off"))
                    VstHostManager.Instance.NoteOff(previewPluginId, 0, previewNote, 0);
                if (GUILayout.Button("Unload"))
                    UnloadPreview();
            }
            EditorGUILayout.EndHorizontal();

            if (hasPreview)
                EditorGUILayout.LabelField("Loaded plugin id", previewPluginId.ToString());

            if (!hostReady)
            {
                EditorGUILayout.HelpBox(
                    "Host is not initialized yet. Scan Default initializes it automatically.",
                    MessageType.Info);
            }
            else if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Load / Note work in Edit Mode, but audible preview needs Play Mode. " +
                    "After Load in Play Mode you can also use Window → VST3 Host → Virtual Controller.",
                    MessageType.Info);
            }
        }

        private IEnumerable<VstHostManager.ScannedPlugin> GetFiltered()
        {
            IEnumerable<VstHostManager.ScannedPlugin> q = scanned;
            if (!string.IsNullOrEmpty(filter))
            {
                q = q.Where(p =>
                    (p.Name != null && p.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.Vendor != null && p.Vendor.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (!string.IsNullOrEmpty(categoryFilter) && categoryFilter != "All")
            {
                var want = categoryFilter == "(none)" ? string.Empty : categoryFilter;
                q = q.Where(p => (p.Category ?? string.Empty) == want);
            }

            return q;
        }

        private void EnsureHost()
        {
            if (!VstHostManager.Instance.IsInitialized)
                VstHostManager.Instance.InitializeFromAudioSettings();
        }

        private void Rescan(string folder)
        {
            EnsureHost();
            scanned.Clear();
            scanned.AddRange(string.IsNullOrEmpty(folder)
                ? VstHostManager.Instance.Scan()
                : VstHostManager.Instance.ScanFolder(folder));
            selectedIndex = 0;
            status = $"Scan found {scanned.Count} plugin class(es).";
        }

        private void RescanExtra()
        {
            EnsureHost();
            scanned.Clear();
            scanned.AddRange(VstHostManager.Instance.Scan());
            foreach (var folder in VstHostProjectSettings.instance.ExtraScanFolders)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;
                scanned.AddRange(VstHostManager.Instance.ScanFolder(folder));
            }

            var unique = scanned
                .GroupBy(p => p.Uid + "|" + p.FilePath)
                .Select(g => g.First())
                .ToList();
            scanned.Clear();
            scanned.AddRange(unique);
            selectedIndex = 0;
            status = $"Scan (with extras) found {scanned.Count} plugin class(es).";
        }

        private void LoadSelected()
        {
            var filtered = GetFiltered().ToList();
            if (selectedIndex < 0 || selectedIndex >= filtered.Count)
                return;

            EnsureHost();
            if (!VstHostManager.Instance.IsInitialized)
            {
                status = "Host initialize failed.";
                return;
            }

            UnloadPreview();
            var p = filtered[selectedIndex];
            previewPluginId = VstHostManager.Instance.CreateInstance(p.FilePath, p.Uid);
            if (previewPluginId < 1)
            {
                status = "Load failed.";
                return;
            }

            EnsurePreviewAudio(previewPluginId);
            status = Application.isPlaying
                ? $"Loaded id={previewPluginId} ({p.Name}). Note On here, or open Virtual Controller."
                : $"Loaded id={previewPluginId} ({p.Name}). Enter Play Mode and Load again for audible preview.";
        }

        private void EnsurePreviewAudio(int pluginId)
        {
            VstHostEditorPreview.EnsureAudio(pluginId);
        }

        private void UnloadPreview()
        {
            if (previewPluginId >= 1 && VstHostManager.Instance.IsInitialized)
                VstHostManager.Instance.DestroyInstance(previewPluginId);
            previewPluginId = -1;
            VstHostEditorPreview.Clear();
        }
    }
}
