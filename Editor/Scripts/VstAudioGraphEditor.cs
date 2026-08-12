using System.Text;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Editor
{
    /// <summary>
    /// Thin Inspector for <see cref="VstAudioGraph"/>: default lists + overview + open window.
    /// No wiring canvas (see <see cref="VstAudioGraphWindow"/>).
    /// </summary>
    [CustomEditor(typeof(VstAudioGraph))]
    internal sealed class VstAudioGraphEditor : UnityEditor.Editor
    {
        private bool overviewFoldout = true;
        private readonly StringBuilder overview = new StringBuilder(256);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var graph = (VstAudioGraph)target;
            EditorGUILayout.Space(6);

            overviewFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(overviewFoldout, "Graph overview (read-only)");
            if (overviewFoldout)
            {
                EditorGUILayout.HelpBox(BuildOverview(graph), MessageType.None);
                if (!string.IsNullOrEmpty(graph.LastArmError))
                    EditorGUILayout.HelpBox("Last arm error: " + graph.LastArmError, MessageType.Warning);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            if (GUILayout.Button("Open Audio Graph Window"))
                VstAudioGraphWindow.Open(graph);
        }

        private string BuildOverview(VstAudioGraph graph)
        {
            overview.Length = 0;
            overview.Append("Armed: ").Append(graph.HasArmedGraph ? "yes" : "no");
            overview.Append("  |  nodes=").Append(graph.Nodes.Count);
            overview.Append("  edges=").Append(graph.Edges.Count);
            overview.Append("  routes=").Append(graph.ChannelRoutes.Count);

            var sidechain = 0;
            var fromSplit = 0;
            for (var i = 0; i < graph.Edges.Count; i++)
            {
                var e = graph.Edges[i];
                if (e.toPort == VstGraphPort.Sidechain)
                    sidechain++;
            }

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i].kind != VstGraphNodeKind.Split)
                    continue;
                var splitId = graph.Nodes[i].id;
                for (var j = 0; j < graph.Edges.Count; j++)
                {
                    if (graph.Edges[j].fromNodeId == splitId)
                        fromSplit++;
                }
            }

            if (sidechain > 0)
                overview.Append("\nSidechain edges: ").Append(sidechain);
            if (fromSplit > 0)
                overview.Append("\nSplit outgoing (send taps): ").Append(fromSplit);

            return overview.ToString();
        }
    }
}
