using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Read-only topology view for <see cref="VstAudioGraph"/>.
    /// Wiring edits stay on Inspector lists / Build* / SetGraph.
    /// </summary>
    internal sealed class VstAudioGraphWindow : EditorWindow
    {
        private static readonly Color SidechainColor = new Color(0.95f, 0.55f, 0.2f, 1f);
        private static readonly Color SendColor = new Color(0.35f, 0.75f, 0.95f, 1f);
        private static readonly Color DryColor = new Color(0.55f, 0.85f, 0.65f, 1f);
        private static readonly Color MainEdgeColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        private VstAudioGraph targetGraph;
        private bool followSelection = true;
        private Vector2 scroll;
        private bool showAscii = true;
        private readonly StringBuilder asciiBuilder = new StringBuilder(512);
        private readonly Dictionary<int, string> nodeLabels = new Dictionary<int, string>(32);

        [MenuItem("Window/VST3 Host/Audio Graph")]
        private static void Open()
        {
            var window = GetWindow<VstAudioGraphWindow>();
            window.titleContent = new GUIContent("Audio Graph");
            window.minSize = new Vector2(480, 320);
            window.Show();
        }

        /// <summary>Open and focus a specific graph (from CustomEditor).</summary>
        public static void Open(VstAudioGraph graph)
        {
            var window = GetWindow<VstAudioGraphWindow>();
            window.titleContent = new GUIContent("Audio Graph");
            window.minSize = new Vector2(480, 320);
            window.followSelection = false;
            window.targetGraph = graph;
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnEditorUpdate()
        {
            if (targetGraph != null)
                Repaint();
        }

        private void OnSelectionChanged()
        {
            if (!followSelection)
                return;

            var go = Selection.activeGameObject;
            if (go != null)
            {
                var graph = go.GetComponent<VstAudioGraph>();
                if (graph != null)
                    targetGraph = graph;
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);

            if (targetGraph == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a GameObject with VstAudioGraph, or assign Target below. " +
                    "Scene search: use the Pick in Scene popup.",
                    MessageType.Info);
                DrawTargetField();
                DrawScenePicker();
                return;
            }

            DrawTargetField();
            DrawArmStatus(targetGraph);
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawNodes(targetGraph);
            EditorGUILayout.Space(6);
            DrawEdges(targetGraph);
            EditorGUILayout.Space(6);
            DrawChannelRoutes(targetGraph);
            EditorGUILayout.Space(6);
            DrawAsciiTopology(targetGraph);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "Read-only. Edit Nodes / Edges / ChannelRoutes in the Inspector, or use Build* / SetGraph. " +
                "Send = Split outgoing edge gain; Sidechain = toPort Sidechain (orange).",
                MessageType.None);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            followSelection = GUILayout.Toggle(
                followSelection, "Follow Selection", EditorStyles.toolbarButton, GUILayout.Width(110));
            if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(40)) && targetGraph != null)
                EditorGUIUtility.PingObject(targetGraph.gameObject);
            if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(50)) && targetGraph != null)
                Selection.activeGameObject = targetGraph.gameObject;
            showAscii = GUILayout.Toggle(
                showAscii, "Text Diagram", EditorStyles.toolbarButton, GUILayout.Width(90));
            GUILayout.FlexibleSpace();
            if (targetGraph != null)
                GUILayout.Label(targetGraph.gameObject.name, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTargetField()
        {
            EditorGUI.BeginChangeCheck();
            var next = (VstAudioGraph)EditorGUILayout.ObjectField(
                "Target", targetGraph, typeof(VstAudioGraph), true);
            if (EditorGUI.EndChangeCheck())
            {
                targetGraph = next;
                followSelection = false;
            }
        }

        private void DrawScenePicker()
        {
            var graphs = Object.FindObjectsOfType<VstAudioGraph>();
            if (graphs == null || graphs.Length == 0)
            {
                EditorGUILayout.HelpBox("No VstAudioGraph in the open scenes.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("In scene", EditorStyles.boldLabel);
            for (var i = 0; i < graphs.Length; i++)
            {
                var g = graphs[i];
                if (g == null)
                    continue;
                if (GUILayout.Button($"{g.gameObject.name}  (nodes={g.Nodes.Count}, edges={g.Edges.Count})"))
                {
                    targetGraph = g;
                    followSelection = false;
                    Selection.activeGameObject = g.gameObject;
                }
            }
        }

        private static void DrawArmStatus(VstAudioGraph graph)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Arm status", EditorStyles.boldLabel);
            var armed = graph.HasArmedGraph;
            var error = graph.LastArmError;
            var status = armed
                ? (string.IsNullOrEmpty(error)
                    ? "Armed (snapshot active)"
                    : "Armed (previous snapshot kept; last arm failed)")
                : (string.IsNullOrEmpty(error)
                    ? "Not armed (empty or never armed)"
                    : "Not armed (last arm failed)");
            EditorGUILayout.LabelField("State", status);
            EditorGUILayout.LabelField("HasArmedGraph", armed ? "true" : "false");
            EditorGUILayout.LabelField("Nodes / Edges", $"{graph.Nodes.Count} / {graph.Edges.Count}");
            if (!string.IsNullOrEmpty(error))
                EditorGUILayout.HelpBox("Last arm error: " + error, MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        private static void DrawNodes(VstAudioGraph graph)
        {
            EditorGUILayout.LabelField($"Nodes ({graph.Nodes.Count})", EditorStyles.boldLabel);
            if (graph.Nodes.Count == 0)
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawHeaderRow("id", "kind", "pluginId", "gain", "bypass");
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var n = graph.Nodes[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(n.id.ToString(), EditorStyles.miniLabel, GUILayout.Width(36));
                GUILayout.Label(n.kind.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label(
                    NeedsPluginId(n.kind) ? n.pluginId.ToString() : "—",
                    EditorStyles.miniLabel, GUILayout.Width(56));
                GUILayout.Label(n.gain.ToString("0.###"), EditorStyles.miniLabel, GUILayout.Width(48));
                GUILayout.Label(
                    SupportsBypass(n.kind) ? (n.bypass ? "yes" : "no") : "—",
                    EditorStyles.miniLabel, GUILayout.Width(48));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEdges(VstAudioGraph graph)
        {
            EditorGUILayout.LabelField($"Edges ({graph.Edges.Count})", EditorStyles.boldLabel);
            if (graph.Edges.Count == 0)
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                return;
            }

            BuildNodeLabels(graph);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("from", EditorStyles.miniBoldLabel, GUILayout.Width(140));
            GUILayout.Label("→ to", EditorStyles.miniBoldLabel, GUILayout.Width(140));
            GUILayout.Label("toPort", EditorStyles.miniBoldLabel, GUILayout.Width(72));
            GUILayout.Label("gain", EditorStyles.miniBoldLabel, GUILayout.Width(48));
            GUILayout.Label("note", EditorStyles.miniBoldLabel, GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            for (var i = 0; i < graph.Edges.Count; i++)
            {
                var e = graph.Edges[i];
                var note = ClassifyEdge(graph, e);
                var color = note == "sidechain" ? SidechainColor
                    : note == "send" ? SendColor
                    : note == "dry" ? DryColor
                    : MainEdgeColor;

                var prev = GUI.contentColor;
                GUI.contentColor = color;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(FormatNodeRef(e.fromNodeId), EditorStyles.miniLabel, GUILayout.Width(140));
                GUILayout.Label(FormatNodeRef(e.toNodeId), EditorStyles.miniLabel, GUILayout.Width(140));
                GUILayout.Label(e.toPort.ToString(), EditorStyles.miniLabel, GUILayout.Width(72));
                GUILayout.Label(e.gain.ToString("0.###"), EditorStyles.miniLabel, GUILayout.Width(48));
                GUILayout.Label(note, EditorStyles.miniLabel, GUILayout.Width(72));
                EditorGUILayout.EndHorizontal();
                GUI.contentColor = prev;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.LabelField(
                "Colors: orange = Sidechain, cyan = Send (Split), green = Dry (Split), gray = Main",
                EditorStyles.miniLabel);
        }

        private static void DrawChannelRoutes(VstAudioGraph graph)
        {
            EditorGUILayout.LabelField($"Channel routes ({graph.ChannelRoutes.Count})", EditorStyles.boldLabel);
            if (graph.ChannelRoutes.Count == 0)
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawHeaderRow("ch", "→ nodeId", "", "", "");
            for (var i = 0; i < graph.ChannelRoutes.Count; i++)
            {
                var r = graph.ChannelRoutes[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(r.channel.ToString(), EditorStyles.miniLabel, GUILayout.Width(36));
                GUILayout.Label(r.nodeId.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAsciiTopology(VstAudioGraph graph)
        {
            if (!showAscii)
                return;

            EditorGUILayout.LabelField("Text diagram", EditorStyles.boldLabel);
            BuildNodeLabels(graph);
            asciiBuilder.Length = 0;
            if (graph.Edges.Count == 0)
            {
                for (var i = 0; i < graph.Nodes.Count; i++)
                {
                    var n = graph.Nodes[i];
                    asciiBuilder.Append(FormatNodeRef(n.id));
                    if (i + 1 < graph.Nodes.Count)
                        asciiBuilder.AppendLine();
                }
            }
            else
            {
                for (var i = 0; i < graph.Edges.Count; i++)
                {
                    var e = graph.Edges[i];
                    var note = ClassifyEdge(graph, e);
                    asciiBuilder.Append(FormatNodeRef(e.fromNodeId));
                    asciiBuilder.Append(" ──");
                    if (e.toPort == VstGraphPort.Sidechain)
                        asciiBuilder.Append("[SC]");
                    else if (note == "send")
                        asciiBuilder.Append("[send×").Append(e.gain.ToString("0.##")).Append(']');
                    else if (note == "dry")
                        asciiBuilder.Append("[dry]");
                    else if (!Mathf.Approximately(e.gain, 1f))
                        asciiBuilder.Append("[g=").Append(e.gain.ToString("0.##")).Append(']');
                    else
                        asciiBuilder.Append('─');
                    asciiBuilder.Append("──► ");
                    asciiBuilder.Append(FormatNodeRef(e.toNodeId));
                    if (e.toPort == VstGraphPort.Sidechain)
                        asciiBuilder.Append(" (sidechain)");
                    if (i + 1 < graph.Edges.Count)
                        asciiBuilder.AppendLine();
                }
            }

            EditorGUILayout.TextArea(asciiBuilder.ToString(), EditorStyles.helpBox);
        }

        private void BuildNodeLabels(VstAudioGraph graph)
        {
            nodeLabels.Clear();
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var n = graph.Nodes[i];
                string label;
                if (NeedsPluginId(n.kind))
                    label = $"{n.kind}#{n.id}(p{n.pluginId})";
                else
                    label = $"{n.kind}#{n.id}";
                if (SupportsBypass(n.kind) && n.bypass)
                    label += " [bypass]";
                nodeLabels[n.id] = label;
            }
        }

        private string FormatNodeRef(int nodeId)
        {
            return nodeLabels.TryGetValue(nodeId, out var label) ? label : $"?#{nodeId}";
        }

        private static string ClassifyEdge(VstAudioGraph graph, VstGraphEdge edge)
        {
            if (edge.toPort == VstGraphPort.Sidechain)
                return "sidechain";

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var n = graph.Nodes[i];
                if (n.id != edge.fromNodeId)
                    continue;
                if (n.kind != VstGraphNodeKind.Split)
                    break;
                // BuildSendReturn uses dry≈1 and send amount on other Split outs.
                return Mathf.Approximately(edge.gain, 1f) ? "dry" : "send";
            }

            return "main";
        }

        private static bool NeedsPluginId(VstGraphNodeKind kind) =>
            kind == VstGraphNodeKind.Instrument || kind == VstGraphNodeKind.Effect;

        private static bool SupportsBypass(VstGraphNodeKind kind) =>
            kind == VstGraphNodeKind.Instrument ||
            kind == VstGraphNodeKind.Effect ||
            kind == VstGraphNodeKind.Gain;

        private static void DrawHeaderRow(string a, string b, string c, string d, string e)
        {
            EditorGUILayout.BeginHorizontal();
            var style = EditorStyles.miniBoldLabel;
            GUILayout.Label(a, style, GUILayout.Width(36));
            GUILayout.Label(b, style, GUILayout.Width(80));
            if (!string.IsNullOrEmpty(c))
                GUILayout.Label(c, style, GUILayout.Width(56));
            if (!string.IsNullOrEmpty(d))
                GUILayout.Label(d, style, GUILayout.Width(48));
            if (!string.IsNullOrEmpty(e))
                GUILayout.Label(e, style, GUILayout.Width(48));
            EditorGUILayout.EndHorizontal();
        }
    }
}
