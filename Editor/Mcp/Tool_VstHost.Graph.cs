#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        internal const string DefaultGraphObjectName = "__VstHostMcpGraph";

        internal static VstAudioGraph FindOrCreateGraph(string? gameObjectName, bool ensureAudioSource = true)
        {
            var name = string.IsNullOrWhiteSpace(gameObjectName)
                ? DefaultGraphObjectName
                : gameObjectName.Trim();
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                go.hideFlags = HideFlags.DontSave;
            }

            if (ensureAudioSource && go.GetComponent<AudioSource>() == null)
                go.AddComponent<AudioSource>();

            var graph = go.GetComponent<VstAudioGraph>();
            if (graph == null)
                graph = go.AddComponent<VstAudioGraph>();

            // Prefer graph over single-plugin filter on the same object.
            var filter = go.GetComponent<VstHostAudioFilter>();
            if (filter != null)
                filter.enabled = false;

            return graph;
        }

        internal static bool TryFindGraph(string? gameObjectName, out VstAudioGraph graph, out string? error)
        {
            graph = null!;
            var name = string.IsNullOrWhiteSpace(gameObjectName)
                ? DefaultGraphObjectName
                : gameObjectName.Trim();
            var go = GameObject.Find(name);
            if (go == null)
            {
                error = $"[Error] GameObject '{name}' not found. Call a vst3-graph-build-* first.";
                return false;
            }

            graph = go.GetComponent<VstAudioGraph>();
            if (graph == null)
            {
                error = $"[Error] VstAudioGraph missing on '{name}'.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Format graph status for tools / resources.</summary>
        public static string FormatGraphStatus(VstAudioGraph graph)
        {
            var sb = new StringBuilder();
            sb.Append($"gameObject={graph.gameObject.name}");
            sb.Append($" armed={graph.HasArmedGraph}");
            sb.Append($" outputGain={graph.OutputGain}");
            sb.Append($" flushDspMidi={graph.FlushDspMidiQueue}");
            if (!string.IsNullOrEmpty(graph.LastArmError))
                sb.Append($" lastArmError={graph.LastArmError}");
            else
                sb.Append(" lastArmError=");

            var nodes = graph.Nodes;
            sb.Append($"\nnodes={nodes?.Count ?? 0}");
            if (nodes != null)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    sb.Append(
                        $"\n  [{i}] id={n.id} kind={n.kind} pluginId={n.pluginId} gain={n.gain:0.###} bypass={n.bypass}");
                }
            }

            var edges = graph.Edges;
            sb.Append($"\nedges={edges?.Count ?? 0}");
            if (edges != null)
            {
                for (var i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    sb.Append(
                        $"\n  [{i}] {e.fromNodeId}:{e.fromPort}→{e.toNodeId}:{e.toPort} gain={e.gain:0.###}");
                }
            }

            var routes = graph.ChannelRoutes;
            sb.Append($"\nchannelRoutes={routes?.Count ?? 0}");
            if (routes != null)
            {
                for (var i = 0; i < routes.Count; i++)
                {
                    var r = routes[i];
                    sb.Append($"\n  ch={r.channel}→nodeId={r.nodeId}");
                }
            }

            return sb.ToString();
        }

        internal static List<int> ParseIdList(string? csv)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(csv))
                return list;
            foreach (var part in csv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                    && id >= 1)
                    list.Add(id);
            }

            return list;
        }

        [AiTool(
            "vst3-graph-status",
            Title = "VST3 / Graph Status",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description(
            "Read VstAudioGraph nodes/edges/ChannelRoutes/armed/LastArmError. " +
            "Empty gameObjectName = __VstHostMcpGraph.")]
        public string GraphStatus
        (
            [Description("GameObject with VstAudioGraph. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryFindGraph(gameObjectName, out var graph, out var error))
                    return error!;
                return $"[Success] vst3-graph-status\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-clear", Title = "VST3 / Graph Clear")]
        [Description("ClearGraph on VstAudioGraph (disarms).")]
        public string GraphClear
        (
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryFindGraph(gameObjectName, out var graph, out var error))
                    return error!;
                graph.ClearGraph();
                return $"[Success] vst3-graph-clear\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-build-parallel", Title = "VST3 / Graph Build Parallel")]
        [Description(
            "BuildParallelInstrumentsThenSerialEffects. instrumentIds/effectIds comma-separated plugin ids. " +
            "Creates __VstHostMcpGraph when needed. Play Mode recommended for audio.")]
        public string GraphBuildParallel
        (
            [Description("Instrument plugin ids, e.g. 1,2")]
            string instrumentIds,
            [Description("Effect plugin ids after mix, e.g. 3")]
            string? effectIds = null,
            [Description("Mix ExternalIn into the bus.")]
            bool mixExternalInput = false,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var instruments = ParseIdList(instrumentIds);
                var effects = ParseIdList(effectIds);
                if (instruments.Count == 0 && !mixExternalInput)
                    return "[Error] vst3-graph-build-parallel: need instrumentIds or mixExternalInput=true.";

                var graph = FindOrCreateGraph(gameObjectName);
                var ok = graph.BuildParallelInstrumentsThenSerialEffects(instruments, effects, mixExternalInput);
                graph.EnsureSilentSourcePlaying();
                return ok
                    ? $"[Success] vst3-graph-build-parallel\n{FormatGraphStatus(graph)}"
                    : $"[Error] vst3-graph-build-parallel failed\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-build-serial", Title = "VST3 / Graph Build Serial")]
        [Description(
            "BuildStrictSerial. slots format: I:pluginId or E:pluginId comma-separated " +
            "(I=Instrument, E=Effect), e.g. I:1,E:2,E:3.")]
        public string GraphBuildSerial
        (
            [Description("Slots e.g. I:1,E:2")]
            string slots,
            [Description("Mix ExternalIn at start.")]
            bool mixExternalInput = false,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryParseSerialSlots(slots, out var list, out var parseErr))
                    return parseErr!;

                var graph = FindOrCreateGraph(gameObjectName);
                var ok = graph.BuildStrictSerial(list, mixExternalInput);
                graph.EnsureSilentSourcePlaying();
                return ok
                    ? $"[Success] vst3-graph-build-serial\n{FormatGraphStatus(graph)}"
                    : $"[Error] vst3-graph-build-serial failed\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-build-send", Title = "VST3 / Graph Build Send/Return")]
        [Description("BuildSendReturn(instrument, sendEffect, sendGain, dryGain).")]
        public string GraphBuildSend
        (
            [Description("Instrument plugin id.")]
            int instrumentPluginId,
            [Description("Send effect plugin id.")]
            int sendEffectPluginId,
            [Description("Send edge gain (default 0.3).")]
            float sendGain = 0.3f,
            [Description("Dry edge gain (default 1).")]
            float dryGain = 1f,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var graph = FindOrCreateGraph(gameObjectName);
                var ok = graph.BuildSendReturn(instrumentPluginId, sendEffectPluginId, sendGain, dryGain);
                graph.EnsureSilentSourcePlaying();
                return ok
                    ? $"[Success] vst3-graph-build-send\n{FormatGraphStatus(graph)}"
                    : $"[Error] vst3-graph-build-send failed\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-build-sidechain", Title = "VST3 / Graph Build Sidechain")]
        [Description(
            "mode=dual: BuildSidechain(mainInst, sidechainInst, effect). " +
            "mode=single: BuildSidechainFromSingleSource(instrument, effect).")]
        public string GraphBuildSidechain
        (
            [Description("dual or single.")]
            string mode = "dual",
            [Description("Main / single-source instrument plugin id.")]
            int instrumentPluginId = 0,
            [Description("Sidechain instrument plugin id (dual mode).")]
            int sidechainInstrumentPluginId = 0,
            [Description("Effect with Aux (e.g. AGain SideChain).")]
            int effectPluginId = 0,
            [Description("Sidechain edge gain for single mode.")]
            float sidechainGain = 1f,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var graph = FindOrCreateGraph(gameObjectName);
                bool ok;
                if (mode.Equals("single", StringComparison.OrdinalIgnoreCase))
                {
                    if (instrumentPluginId < 1 || effectPluginId < 1)
                        return "[Error] single mode needs instrumentPluginId and effectPluginId.";
                    ok = graph.BuildSidechainFromSingleSource(
                        instrumentPluginId, effectPluginId, sidechainGain);
                }
                else
                {
                    if (instrumentPluginId < 1 || sidechainInstrumentPluginId < 1 || effectPluginId < 1)
                        return "[Error] dual mode needs instrumentPluginId, sidechainInstrumentPluginId, effectPluginId.";
                    ok = graph.BuildSidechain(
                        instrumentPluginId, sidechainInstrumentPluginId, effectPluginId);
                }

                graph.EnsureSilentSourcePlaying();
                return ok
                    ? $"[Success] vst3-graph-build-sidechain mode={mode}\n{FormatGraphStatus(graph)}"
                    : $"[Error] vst3-graph-build-sidechain failed\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-set", Title = "VST3 / Graph Set")]
        [Description(
            "Low-level SetGraph. nodesCsv: id:Kind[:pluginId[:gain[:bypass]]] " +
            "(Kind=ExternalIn|Instrument|Effect|Mix|Split|Gain|Output). " +
            "edgesCsv: from>to[:Main|Sidechain[:gain]]. Prefer Build* tools.")]
        public string GraphSet
        (
            [Description("Nodes CSV.")]
            string nodesCsv,
            [Description("Edges CSV.")]
            string edgesCsv,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryParseNodes(nodesCsv, out var nodes, out var nErr))
                    return nErr!;
                if (!TryParseEdges(edgesCsv, out var edges, out var eErr))
                    return eErr!;

                var graph = FindOrCreateGraph(gameObjectName);
                var ok = graph.SetGraph(nodes, edges);
                graph.EnsureSilentSourcePlaying();
                return ok
                    ? $"[Success] vst3-graph-set\n{FormatGraphStatus(graph)}"
                    : $"[Error] vst3-graph-set arm failed\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-connect", Title = "VST3 / Graph Connect")]
        [Description(
            "Append one Main or Sidechain edge to the existing graph and re-arm via SetGraph.")]
        public string GraphConnect
        (
            [Description("From node id.")]
            int fromNodeId,
            [Description("To node id.")]
            int toNodeId,
            [Description("Main or Sidechain.")]
            string toPort = "Main",
            [Description("Edge gain.")]
            float gain = 1f,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryFindGraph(gameObjectName, out var graph, out var error))
                    return error!;
                if (!TryParsePort(toPort, out var port, out var portErr))
                    return portErr!;

                var nodes = graph.Nodes.ToList();
                var edges = graph.Edges.ToList();
                edges.Add(new VstGraphEdge
                {
                    fromNodeId = fromNodeId,
                    fromPort = VstGraphPort.Main,
                    toNodeId = toNodeId,
                    toPort = port,
                    gain = gain,
                });
                var ok = graph.SetGraph(nodes, edges);
                return ok
                    ? $"[Success] vst3-graph-connect {fromNodeId}→{toNodeId}:{port}\n{FormatGraphStatus(graph)}"
                    : $"[Error] vst3-graph-connect arm failed\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-bypass", Title = "VST3 / Graph Bypass")]
        [Description("SetBypass by nodeId, or SetBypassByPluginId when pluginId>0 and nodeId=0.")]
        public string GraphBypass
        (
            [Description("Bypass on/off.")]
            bool bypass,
            [Description("Node id (preferred).")]
            int nodeId = 0,
            [Description("Plugin id when nodeId=0.")]
            int pluginId = 0,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryFindGraph(gameObjectName, out var graph, out var error))
                    return error!;

                bool ok;
                if (nodeId != 0)
                    ok = graph.SetBypass(nodeId, bypass);
                else if (pluginId >= 1)
                    ok = graph.SetBypassByPluginId(pluginId, bypass);
                else
                    return "[Error] Provide nodeId or pluginId.";

                return ok
                    ? $"[Success] vst3-graph-bypass bypass={bypass} nodeId={nodeId} pluginId={pluginId}\n{FormatGraphStatus(graph)}"
                    : $"[Error] vst3-graph-bypass: target not found.";
            });
        }

        [AiTool("vst3-graph-set-node-gain", Title = "VST3 / Graph Set Node Gain")]
        [Description("SetNodeGain(nodeId, gain).")]
        public string GraphSetNodeGain
        (
            [Description("Node id.")]
            int nodeId,
            [Description("Gain.")]
            float gain,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryFindGraph(gameObjectName, out var graph, out var error))
                    return error!;
                if (!graph.SetNodeGain(nodeId, gain))
                    return $"[Error] vst3-graph-set-node-gain: nodeId={nodeId} not found.";
                return $"[Success] vst3-graph-set-node-gain nodeId={nodeId} gain={gain}\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-set-edge-gain", Title = "VST3 / Graph Set Edge Gain")]
        [Description("SetEdgeGain(from, to, toPort, gain) — e.g. live send amount.")]
        public string GraphSetEdgeGain
        (
            [Description("From node id.")]
            int fromNodeId,
            [Description("To node id.")]
            int toNodeId,
            [Description("Main or Sidechain.")]
            string toPort = "Main",
            [Description("Edge gain.")]
            float gain = 1f,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryFindGraph(gameObjectName, out var graph, out var error))
                    return error!;
                if (!TryParsePort(toPort, out var port, out var portErr))
                    return portErr!;
                if (!graph.SetEdgeGain(fromNodeId, toNodeId, port, gain))
                    return $"[Error] vst3-graph-set-edge-gain: edge {fromNodeId}→{toNodeId}:{port} not found.";
                return
                    $"[Success] vst3-graph-set-edge-gain {fromNodeId}→{toNodeId}:{port} gain={gain}\n" +
                    FormatGraphStatus(graph);
            });
        }

        [AiTool("vst3-graph-channel-routes", Title = "VST3 / Graph Channel Routes")]
        [Description(
            "SetChannelRoutes: MIDI channel → instrument node id. Format '0:1,1:2' (ch:nodeId). Empty clears.")]
        public string GraphChannelRoutes
        (
            [Description("Routes e.g. 0:1,1:3. Empty clears.")]
            string? routes = null,
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryFindGraph(gameObjectName, out var graph, out var error))
                    return error!;
                if (!TryParseGraphChannelRoutes(routes ?? string.Empty, out var list, out var parseErr))
                    return parseErr!;
                graph.SetChannelRoutes(list);
                return $"[Success] vst3-graph-channel-routes\n{FormatGraphStatus(graph)}";
            });
        }

        [AiTool("vst3-graph-arm", Title = "VST3 / Graph Arm")]
        [Description("TryArmGraph and report HasArmedGraph / LastArmError. Play Mode recommended.")]
        public string GraphArm
        (
            [Description("GameObject name. Empty = __VstHostMcpGraph.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryFindGraph(gameObjectName, out var graph, out var error))
                    return error!;
                var ok = graph.TryArmGraph(logFailures: true);
                graph.EnsureSilentSourcePlaying();
                return ok
                    ? $"[Success] vst3-graph-arm\n{FormatGraphStatus(graph)}"
                    : $"[Error] vst3-graph-arm failed\n{FormatGraphStatus(graph)}";
            });
        }

        static bool TryParseSerialSlots(
            string text,
            out List<VstGraphBuildSlot> slots,
            out string? error)
        {
            slots = new List<VstGraphBuildSlot>();
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "[Error] slots is required (e.g. I:1,E:2).";
                return false;
            }

            foreach (var raw in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim();
                var sep = token.IndexOf(':');
                if (sep < 0)
                {
                    error = $"[Error] slot '{token}' must be I:id or E:id.";
                    return false;
                }

                var kind = token.Substring(0, sep).Trim();
                if (!int.TryParse(token.Substring(sep + 1).Trim(), out var pluginId) || pluginId < 1)
                {
                    error = $"[Error] invalid pluginId in '{token}'.";
                    return false;
                }

                if (kind.Equals("I", StringComparison.OrdinalIgnoreCase)
                    || kind.Equals("Instrument", StringComparison.OrdinalIgnoreCase)
                    || kind.Equals("Inst", StringComparison.OrdinalIgnoreCase))
                    slots.Add(VstGraphBuildSlot.Instrument(pluginId));
                else if (kind.Equals("E", StringComparison.OrdinalIgnoreCase)
                         || kind.Equals("Effect", StringComparison.OrdinalIgnoreCase)
                         || kind.Equals("Fx", StringComparison.OrdinalIgnoreCase))
                    slots.Add(VstGraphBuildSlot.Effect(pluginId));
                else
                {
                    error = $"[Error] slot kind must be I or E (got '{kind}').";
                    return false;
                }
            }

            if (slots.Count == 0)
            {
                error = "[Error] no valid slots parsed.";
                return false;
            }

            return true;
        }

        static bool TryParsePort(string text, out VstGraphPort port, out string? error)
        {
            port = VstGraphPort.Main;
            error = null;
            if (string.IsNullOrWhiteSpace(text)
                || text.Equals("Main", StringComparison.OrdinalIgnoreCase))
                return true;
            if (text.Equals("Sidechain", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Aux", StringComparison.OrdinalIgnoreCase)
                || text.Equals("SC", StringComparison.OrdinalIgnoreCase))
            {
                port = VstGraphPort.Sidechain;
                return true;
            }

            error = "[Error] toPort must be Main or Sidechain.";
            return false;
        }

        static bool TryParseGraphChannelRoutes(
            string text,
            out List<VstAudioGraph.ChannelRoute> routes,
            out string? error)
        {
            routes = new List<VstAudioGraph.ChannelRoute>();
            error = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            foreach (var raw in text.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim();
                var sep = token.IndexOf(':');
                if (sep < 0)
                    sep = token.IndexOf('=');
                if (sep < 0
                    || !int.TryParse(token.Substring(0, sep).Trim(), out var ch)
                    || !int.TryParse(token.Substring(sep + 1).Trim(), out var nodeId))
                {
                    error = $"[Error] route '{token}' must be channel:nodeId.";
                    return false;
                }

                if (ch < 0 || ch > 15)
                {
                    error = $"[Error] channel must be 0–15 (got {ch}).";
                    return false;
                }

                routes.Add(new VstAudioGraph.ChannelRoute { channel = ch, nodeId = nodeId });
            }

            return true;
        }

        static bool TryParseNodes(string csv, out List<VstGraphNode> nodes, out string? error)
        {
            nodes = new List<VstGraphNode>();
            error = null;
            if (string.IsNullOrWhiteSpace(csv))
            {
                error = "[Error] nodesCsv is required.";
                return false;
            }

            foreach (var raw in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = raw.Trim().Split(':');
                if (parts.Length < 2
                    || !int.TryParse(parts[0].Trim(), out var id)
                    || !TryParseNodeKind(parts[1].Trim(), out var kind))
                {
                    error = $"[Error] node '{raw}' must be id:Kind[:pluginId[:gain[:bypass]]].";
                    return false;
                }

                var pluginId = 0;
                var gain = 1f;
                var bypass = false;
                if (parts.Length >= 3)
                    int.TryParse(parts[2].Trim(), out pluginId);
                if (parts.Length >= 4)
                    float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out gain);
                if (parts.Length >= 5)
                    bypass = parts[4].Trim() is "1" or "true" or "True";

                var node = VstGraphNode.Create(id, kind, pluginId, gain);
                node.bypass = bypass;
                nodes.Add(node);
            }

            return nodes.Count > 0;
        }

        static bool TryParseNodeKind(string text, out VstGraphNodeKind kind)
        {
            kind = VstGraphNodeKind.Mix;
            return Enum.TryParse(text, ignoreCase: true, out kind);
        }

        static bool TryParseEdges(string csv, out List<VstGraphEdge> edges, out string? error)
        {
            edges = new List<VstGraphEdge>();
            error = null;
            if (string.IsNullOrWhiteSpace(csv))
            {
                error = "[Error] edgesCsv is required.";
                return false;
            }

            foreach (var raw in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim();
                var arrow = token.IndexOf('>');
                if (arrow < 0)
                {
                    error = $"[Error] edge '{token}' must be from>to[:Port[:gain]].";
                    return false;
                }

                if (!int.TryParse(token.Substring(0, arrow).Trim(), out var from))
                {
                    error = $"[Error] bad from id in '{token}'.";
                    return false;
                }

                var rest = token.Substring(arrow + 1).Trim();
                var parts = rest.Split(':');
                if (parts.Length < 1 || !int.TryParse(parts[0].Trim(), out var to))
                {
                    error = $"[Error] bad to id in '{token}'.";
                    return false;
                }

                var port = VstGraphPort.Main;
                var gain = 1f;
                if (parts.Length >= 2 && !TryParsePort(parts[1].Trim(), out port, out _))
                    port = VstGraphPort.Main;
                if (parts.Length >= 3)
                    float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out gain);

                edges.Add(new VstGraphEdge
                {
                    fromNodeId = from,
                    fromPort = VstGraphPort.Main,
                    toNodeId = to,
                    toPort = port,
                    gain = gain,
                });
            }

            return edges.Count > 0;
        }
    }
}
