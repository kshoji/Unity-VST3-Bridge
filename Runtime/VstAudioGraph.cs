using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Node-graph DSP host driven by <see cref="OnAudioFilterRead"/>.
    /// Do not also enable <see cref="VstHostAudioFilter"/> / <see cref="VstPluginChain"/> on the same AudioSource.
    /// V1 is a stereo DAG (see Documentation~/audio-graph-plan.md §14).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class VstAudioGraph : MonoBehaviour
    {
        public const int MaxNodes = 32;

        [Serializable]
        public struct ChannelRoute
        {
            [Range(0, 15)] public int channel;
            [Tooltip("Instrument node id (not list index).")]
            public int nodeId;
        }

        [SerializeField] private List<VstGraphNode> nodes = new List<VstGraphNode>();
        [SerializeField] private List<VstGraphEdge> edges = new List<VstGraphEdge>();
        [SerializeField] private List<ChannelRoute> channelRoutes = new List<ChannelRoute>();
        [SerializeField] private float outputGain = 1f;
        [SerializeField] private bool autoPlaySilentSource = true;
        [SerializeField] private bool flushDspMidiQueue = true;

        private AudioSource audioSource;
        private int armed = 1;
        private bool warnedNotReady;
        private bool warnedPeerComponent;
        private string pendingArmWarning;
        private string lastArmError;
        private int nextNodeId = 1;
        private int editVersion;
        private int armedEditVersion = -1;

        // Scratch: one L/R pair per node index + process temps. Grown on main / first use to BlockSize.
        private float[][] nodeL = new float[MaxNodes][];
        private float[][] nodeR = new float[MaxNodes][];
        private float[] tempInL = Array.Empty<float>();
        private float[] tempInR = Array.Empty<float>();
        private float[] tempOutL = Array.Empty<float>();
        private float[] tempOutR = Array.Empty<float>();
        private float[] tempScL = Array.Empty<float>();
        private float[] tempScR = Array.Empty<float>();
        private float[] externalL = Array.Empty<float>();
        private float[] externalR = Array.Empty<float>();
        private float[] finalL = Array.Empty<float>();
        private float[] finalR = Array.Empty<float>();

        private ArmedSnapshot snapA;
        private ArmedSnapshot snapB;
        private bool snapWriteA = true;
        private ArmedSnapshot armedSnap;

        /// <summary>Main-thread node list. Prefer <see cref="SetGraph"/> while playing.</summary>
        public IList<VstGraphNode> Nodes => nodes;

        /// <summary>Main-thread edge list. Prefer <see cref="SetGraph"/> while playing.</summary>
        public IList<VstGraphEdge> Edges => edges;

        /// <summary>Main-thread channel routes. Prefer <see cref="SetChannelRoutes"/> while playing.</summary>
        public IList<ChannelRoute> ChannelRoutes => channelRoutes;

        public float OutputGain
        {
            get => outputGain;
            set => outputGain = value;
        }

        public bool FlushDspMidiQueue
        {
            get => flushDspMidiQueue;
            set => flushDspMidiQueue = value;
        }

        /// <summary>Last successful arm snapshot is active when non-null.</summary>
        public bool HasArmedGraph => Volatile.Read(ref armedSnap) != null;

        public void ClearGraph()
        {
            nodes.Clear();
            edges.Clear();
            MarkDirty();
            Volatile.Write(ref armedSnap, null);
            armedEditVersion = editVersion;
            pendingArmWarning = null;
        }

        /// <summary>Replace nodes/edges and arm immediately (main thread).</summary>
        public bool SetGraph(IEnumerable<VstGraphNode> newNodes, IEnumerable<VstGraphEdge> newEdges)
        {
            nodes.Clear();
            edges.Clear();
            if (newNodes != null)
                nodes.AddRange(newNodes);
            if (newEdges != null)
                edges.AddRange(newEdges);
            RefreshNextNodeId();
            MarkDirty();
            return TryArmGraph(logFailures: true);
        }

        public void SetChannelRoutes(IEnumerable<ChannelRoute> newRoutes)
        {
            channelRoutes.Clear();
            if (newRoutes != null)
                channelRoutes.AddRange(newRoutes);
            MarkDirty();
            TryArmGraph(logFailures: true);
        }

        /// <summary>Sets bypass on the first node with <paramref name="nodeId"/>.</summary>
        public bool SetBypass(int nodeId, bool bypass)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.id != nodeId)
                    continue;
                n.bypass = bypass;
                nodes[i] = n;
                MarkDirty();
                TryArmGraph(logFailures: true);
                return true;
            }

            return false;
        }

        /// <summary>Sets bypass on the first Instrument/Effect with <paramref name="pluginId"/>.</summary>
        public bool SetBypassByPluginId(int pluginId, bool bypass)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.pluginId != pluginId)
                    continue;
                if (n.kind != VstGraphNodeKind.Instrument && n.kind != VstGraphNodeKind.Effect)
                    continue;
                n.bypass = bypass;
                nodes[i] = n;
                MarkDirty();
                TryArmGraph(logFailures: true);
                return true;
            }

            return false;
        }

        public bool SetNodeGain(int nodeId, float gain)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.id != nodeId)
                    continue;
                n.gain = gain;
                nodes[i] = n;
                MarkDirty();
                TryArmGraph(logFailures: true);
                return true;
            }

            return false;
        }

        /// <summary>Next stable node id for manual graph editing.</summary>
        public int AllocateNodeId() => nextNodeId++;

        private void MarkDirty() => editVersion++;

        /// <summary>
        /// Resolves instrument plugin id for a MIDI channel (route → first instrument).
        /// Reads the armed snapshot (DSP / MIDI scheduling safe).
        /// </summary>
        public int ResolveInstrumentPluginId(int channel)
        {
            var snap = Volatile.Read(ref armedSnap);
            if (snap == null)
                return -1;

            for (var i = 0; i < snap.routes.Length; i++)
            {
                var route = snap.routes[i];
                if (route.channel != channel)
                    continue;
                if (!snap.TryGetNodeIndex(route.nodeId, out var idx))
                    continue;
                var node = snap.nodes[idx];
                if (!node.bypass && node.kind == VstGraphNodeKind.Instrument && node.pluginId >= 1)
                    return node.pluginId;
            }

            for (var i = 0; i < snap.nodes.Length; i++)
            {
                var node = snap.nodes[i];
                if (!node.bypass && node.kind == VstGraphNodeKind.Instrument && node.pluginId >= 1)
                    return node.pluginId;
            }

            return -1;
        }

        /// <summary>
        /// Parallel instruments → mix → serial effects → output (Chain default MixMode equivalent).
        /// </summary>
        public bool BuildParallelInstrumentsThenSerialEffects(
            IReadOnlyList<int> instrumentPluginIds,
            IReadOnlyList<int> effectPluginIds,
            bool mixExternalInput = false)
        {
            var builtNodes = new List<VstGraphNode>();
            var builtEdges = new List<VstGraphEdge>();
            var id = 1;

            int? externalId = null;
            if (mixExternalInput)
            {
                externalId = id;
                builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.ExternalIn));
            }

            var instrumentIds = new List<int>();
            if (instrumentPluginIds != null)
            {
                for (var i = 0; i < instrumentPluginIds.Count; i++)
                {
                    var pid = instrumentPluginIds[i];
                    if (pid < 1)
                        continue;
                    instrumentIds.Add(id);
                    builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Instrument, pid));
                }
            }

            var mixId = id;
            builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Mix));

            if (externalId.HasValue)
                builtEdges.Add(VstGraphEdge.Main(externalId.Value, mixId));
            for (var i = 0; i < instrumentIds.Count; i++)
                builtEdges.Add(VstGraphEdge.Main(instrumentIds[i], mixId));

            // Mix with zero instrument edges is invalid — seed silence via a Gain from ExternalIn or fail.
            if (instrumentIds.Count == 0 && !externalId.HasValue)
            {
                // Allow effect-only: silent Mix still needs ≥1 input — use a muted Gain from a dummy path.
                // Prefer requiring at least one source: add a Gain node fed by nothing is invalid.
                // Insert ExternalIn even when mixExternalInput is false would change semantics.
                // Effect-only: create Gain(0) is wrong. Use Instrument-less Mix seeded by ExternalIn always? No.
                // Spec: Mix Main 入 ≥ 1. For effect-only chain, add a silent Instrument-equivalent:
                // a Gain node with no input is also invalid. Best: require ≥1 instrument OR external.
                Debug.LogWarning(
                    "[VstAudioGraph] BuildParallel requires at least one instrument plugin id or mixExternalInput.");
                return false;
            }

            if (instrumentIds.Count == 0 && externalId.HasValue)
            {
                // ok: ExternalIn → Mix
            }

            var cursor = mixId;
            if (effectPluginIds != null)
            {
                for (var i = 0; i < effectPluginIds.Count; i++)
                {
                    var pid = effectPluginIds[i];
                    if (pid < 1)
                        continue;
                    var fxId = id;
                    builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Effect, pid));
                    builtEdges.Add(VstGraphEdge.Main(cursor, fxId));
                    cursor = fxId;
                }
            }

            var outId = id;
            builtNodes.Add(VstGraphNode.Create(id, VstGraphNodeKind.Output));
            builtEdges.Add(VstGraphEdge.Main(cursor, outId));

            return SetGraph(builtNodes, builtEdges);
        }

        /// <summary>
        /// Strict serial bus (Chain StrictSerial equivalent): instruments add into the bus; effects replace it.
        /// </summary>
        public bool BuildStrictSerial(IReadOnlyList<VstGraphBuildSlot> slots, bool mixExternalInput = false)
        {
            if (slots == null || slots.Count == 0)
            {
                Debug.LogWarning("[VstAudioGraph] BuildStrictSerial requires at least one slot.");
                return false;
            }

            var builtNodes = new List<VstGraphNode>();
            var builtEdges = new List<VstGraphEdge>();
            var id = 1;

            int? busId = null;
            if (mixExternalInput)
            {
                var extId = id;
                builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.ExternalIn));
                busId = id;
                builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Mix));
                builtEdges.Add(VstGraphEdge.Main(extId, busId.Value));
            }

            var hasSignal = mixExternalInput;

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.pluginId < 1)
                    continue;

                if (slot.isInstrument)
                {
                    var instId = id;
                    builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Instrument, slot.pluginId, slot.gain));
                    if (!hasSignal)
                    {
                        busId = id;
                        builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Mix));
                        builtEdges.Add(VstGraphEdge.Main(instId, busId.Value));
                        hasSignal = true;
                    }
                    else
                    {
                        var nextMix = id;
                        builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Mix));
                        builtEdges.Add(VstGraphEdge.Main(busId.Value, nextMix));
                        builtEdges.Add(VstGraphEdge.Main(instId, nextMix));
                        busId = nextMix;
                    }
                }
                else
                {
                    if (!hasSignal || !busId.HasValue)
                        continue;
                    var fxId = id;
                    builtNodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Effect, slot.pluginId, slot.gain));
                    builtEdges.Add(VstGraphEdge.Main(busId.Value, fxId));
                    busId = fxId;
                }
            }

            if (!hasSignal || !busId.HasValue)
            {
                Debug.LogWarning("[VstAudioGraph] BuildStrictSerial produced no audible path.");
                return false;
            }

            var outId = id;
            builtNodes.Add(VstGraphNode.Create(id, VstGraphNodeKind.Output));
            builtEdges.Add(VstGraphEdge.Main(busId.Value, outId));
            return SetGraph(builtNodes, builtEdges);
        }

        /// <summary>
        /// Send/Return: instrument → Split → dry (edge gain) + send effect → Mix → Output.
        /// Send amount is the edge gain into the effect (default 0.3). No loudness normalization on Mix.
        /// </summary>
        public bool BuildSendReturn(
            int instrumentPluginId,
            int sendEffectPluginId,
            float sendGain = 0.3f,
            float dryGain = 1f)
        {
            if (instrumentPluginId < 1 || sendEffectPluginId < 1)
            {
                Debug.LogWarning("[VstAudioGraph] BuildSendReturn requires valid instrument and effect plugin ids.");
                return false;
            }

            var id = 1;
            var instId = id;
            var nodes = new List<VstGraphNode>
            {
                VstGraphNode.Create(id++, VstGraphNodeKind.Instrument, instrumentPluginId),
            };
            var splitId = id;
            nodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Split));
            var fxId = id;
            nodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Effect, sendEffectPluginId));
            var mixId = id;
            nodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Mix));
            var outId = id;
            nodes.Add(VstGraphNode.Create(id, VstGraphNodeKind.Output));

            var edges = new List<VstGraphEdge>
            {
                VstGraphEdge.Main(instId, splitId),
                VstGraphEdge.Main(splitId, mixId, dryGain),
                VstGraphEdge.Main(splitId, fxId, sendGain),
                VstGraphEdge.Main(fxId, mixId),
                VstGraphEdge.Main(mixId, outId),
            };
            return SetGraph(nodes, edges);
        }

        /// <summary>
        /// Sidechain: main instrument → Mix → Effect(main); sidechain instrument → Effect(sidechain) → Output.
        /// Use an Aux-capable effect (e.g. SDK AGain SideChain). Plugins without Aux ignore the sidechain bus.
        /// </summary>
        public bool BuildSidechain(
            int mainInstrumentPluginId,
            int sidechainInstrumentPluginId,
            int effectPluginId)
        {
            if (mainInstrumentPluginId < 1 || sidechainInstrumentPluginId < 1 || effectPluginId < 1)
            {
                Debug.LogWarning("[VstAudioGraph] BuildSidechain requires valid plugin ids.");
                return false;
            }

            var id = 1;
            var mainInstId = id;
            var nodes = new List<VstGraphNode>
            {
                VstGraphNode.Create(id++, VstGraphNodeKind.Instrument, mainInstrumentPluginId),
            };
            var scInstId = id;
            nodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Instrument, sidechainInstrumentPluginId));
            var mixId = id;
            nodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Mix));
            var fxId = id;
            nodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Effect, effectPluginId));
            var outId = id;
            nodes.Add(VstGraphNode.Create(id, VstGraphNodeKind.Output));

            var edges = new List<VstGraphEdge>
            {
                VstGraphEdge.Main(mainInstId, mixId),
                VstGraphEdge.Main(mixId, fxId),
                VstGraphEdge.Sidechain(scInstId, fxId),
                VstGraphEdge.Main(fxId, outId),
            };
            return SetGraph(nodes, edges);
        }

        /// <summary>
        /// Sidechain from one instrument: Split feeds Effect main and sidechain (AGain SideChain energy check).
        /// </summary>
        public bool BuildSidechainFromSingleSource(int instrumentPluginId, int effectPluginId, float sidechainGain = 1f)
        {
            if (instrumentPluginId < 1 || effectPluginId < 1)
            {
                Debug.LogWarning("[VstAudioGraph] BuildSidechainFromSingleSource requires valid plugin ids.");
                return false;
            }

            var id = 1;
            var instId = id;
            var nodes = new List<VstGraphNode>
            {
                VstGraphNode.Create(id++, VstGraphNodeKind.Instrument, instrumentPluginId),
            };
            var splitId = id;
            nodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Split));
            var fxId = id;
            nodes.Add(VstGraphNode.Create(id++, VstGraphNodeKind.Effect, effectPluginId));
            var outId = id;
            nodes.Add(VstGraphNode.Create(id, VstGraphNodeKind.Output));

            var edges = new List<VstGraphEdge>
            {
                VstGraphEdge.Main(instId, splitId),
                VstGraphEdge.Main(splitId, fxId, 1f),
                VstGraphEdge.Sidechain(splitId, fxId, sidechainGain),
                VstGraphEdge.Main(fxId, outId),
            };
            return SetGraph(nodes, edges);
        }

        /// <summary>Updates gain on the first matching edge (e.g. send amount). Arms immediately.</summary>
        public bool SetEdgeGain(int fromNodeId, int toNodeId, VstGraphPort toPort, float gain)
        {
            for (var i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                if (e.fromNodeId != fromNodeId || e.toNodeId != toNodeId || e.toPort != toPort)
                    continue;
                e.gain = gain;
                edges[i] = e;
                MarkDirty();
                TryArmGraph(logFailures: true);
                return true;
            }

            return false;
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            RefreshNextNodeId();
            TryArmGraph(logFailures: true);
            if (autoPlaySilentSource)
                EnsureSilentSourcePlaying();
        }

        private void OnEnable()
        {
            Volatile.Write(ref armed, 1);
            WarnIfPeerAudioComponents();
            TryArmGraph(logFailures: true);
            if (autoPlaySilentSource)
                EnsureSilentSourcePlaying();
        }

        private void OnDisable()
        {
            Volatile.Write(ref armed, 0);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshNextNodeId();
            MarkDirty();
            // Edit Mode + Play Mode Inspector edits: validate / arm without spamming logs every keystroke.
            TryArmGraph(logFailures: false);
        }
#endif

        public void EnsureSilentSourcePlaying()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                return;

            if (audioSource.clip == null)
            {
                const int len = 256;
                var clip = AudioClip.Create("VstAudioGraphSilence", len, 1, AudioSettings.outputSampleRate, false);
                var zeros = new float[len];
                clip.SetData(zeros, 0);
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
            }

            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        private void LateUpdate()
        {
            // Always re-arm so direct IList / Inspector mutations apply within one frame (Chain parity).
            // Warning text is de-duplicated via lastArmError.
            TryArmGraph(logFailures: true);
            EnsureScratchCapacityMainThread();
            VstHostDspMidiQueue.Shared.PumpMainThreadDiagnostics();
            VstHostAudioDiagnostics.PumpMainThreadDiagnostics();
            VstHostActivity.PumpMainThread();

            if (pendingArmWarning != null)
            {
                Debug.LogWarning(pendingArmWarning);
                pendingArmWarning = null;
            }

            if (warnedNotReady)
            {
                warnedNotReady = false;
                Debug.LogWarning("[VstAudioGraph] Host not initialized; audio skipped.");
            }
        }

        private void WarnIfPeerAudioComponents()
        {
            if (warnedPeerComponent)
                return;
            var filter = GetComponent<VstHostAudioFilter>();
            var chain = GetComponent<VstPluginChain>();
            if ((filter != null && filter.enabled) || (chain != null && chain.enabled))
            {
                warnedPeerComponent = true;
                Debug.LogWarning(
                    "[VstAudioGraph] Disable VstHostAudioFilter / VstPluginChain on the same GameObject; " +
                    "only one audio path should run.");
            }
        }

        private void RefreshNextNodeId()
        {
            var max = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].id > max)
                    max = nodes[i].id;
            }

            nextNodeId = max + 1;
        }

        private void EnsureScratchCapacityMainThread()
        {
            var host = VstHostManager.Instance;
            var frames = host != null && host.IsInitialized ? host.BlockSize : 0;
            if (frames <= 0)
                frames = 1024;
            EnsureCapacity(frames);
        }

        private void EnsureCapacity(int frames)
        {
            for (var i = 0; i < MaxNodes; i++)
            {
                if (nodeL[i] == null || nodeL[i].Length < frames)
                    nodeL[i] = new float[frames];
                if (nodeR[i] == null || nodeR[i].Length < frames)
                    nodeR[i] = new float[frames];
            }

            if (tempInL.Length < frames) tempInL = new float[frames];
            if (tempInR.Length < frames) tempInR = new float[frames];
            if (tempOutL.Length < frames) tempOutL = new float[frames];
            if (tempOutR.Length < frames) tempOutR = new float[frames];
            if (tempScL.Length < frames) tempScL = new float[frames];
            if (tempScR.Length < frames) tempScR = new float[frames];
            if (externalL.Length < frames) externalL = new float[frames];
            if (externalR.Length < frames) externalR = new float[frames];
            if (finalL.Length < frames) finalL = new float[frames];
            if (finalR.Length < frames) finalR = new float[frames];
        }

        /// <summary>
        /// Validate + publish immutable snapshot. On failure keeps the previous armed graph.
        /// Main thread only.
        /// </summary>
        public bool TryArmGraph(bool logFailures)
        {
            if (!TryBuildArmedSnapshot(out var next, out var error))
            {
                if (logFailures && !string.IsNullOrEmpty(error) && error != lastArmError)
                {
                    pendingArmWarning = "[VstAudioGraph] Arm failed: " + error;
                    lastArmError = error;
                }

                armedEditVersion = editVersion;
                return false;
            }

            Volatile.Write(ref armedSnap, next);
            armedEditVersion = editVersion;
            pendingArmWarning = null;
            lastArmError = null;
            return true;
        }

        private bool TryBuildArmedSnapshot(out ArmedSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            var nodeCount = nodes != null ? nodes.Count : 0;
            if (nodeCount == 0)
            {
                // Empty graph → silence (explicit clear).
                snapshot = null;
                return true;
            }

            if (nodeCount > MaxNodes)
            {
                error = $"node count {nodeCount} exceeds MaxNodes ({MaxNodes}).";
                return false;
            }

            var nodeArr = new VstGraphNode[nodeCount];
            var idToIndex = new Dictionary<int, int>(nodeCount);
            var outputCount = 0;
            var externalCount = 0;
            var outputIndex = -1;
            var externalIndex = -1;

            for (var i = 0; i < nodeCount; i++)
            {
                var n = nodes[i];
                if (idToIndex.ContainsKey(n.id))
                {
                    error = $"duplicate node id {n.id}.";
                    return false;
                }

                idToIndex[n.id] = i;
                nodeArr[i] = n;
                if (n.kind == VstGraphNodeKind.Output)
                {
                    outputCount++;
                    outputIndex = i;
                }
                else if (n.kind == VstGraphNodeKind.ExternalIn)
                {
                    externalCount++;
                    externalIndex = i;
                }
            }

            if (outputCount != 1)
            {
                error = $"Output count must be exactly 1 (got {outputCount}).";
                return false;
            }

            if (externalCount > 1)
            {
                error = $"ExternalIn count must be 0 or 1 (got {externalCount}).";
                return false;
            }

            var edgeCount = edges != null ? edges.Count : 0;
            var mainInCounts = new int[nodeCount];
            var sideInCounts = new int[nodeCount];
            var flatMainSrc = new List<int>(edgeCount);
            var flatMainGain = new List<float>(edgeCount);
            var flatMainTo = new List<int>(edgeCount);
            var sideSrc = new int[nodeCount];
            var sideGain = new float[nodeCount];
            for (var i = 0; i < nodeCount; i++)
            {
                sideSrc[i] = -1;
                sideGain[i] = 1f;
            }

            var adj = new List<int>[nodeCount];
            var indegree = new int[nodeCount];
            for (var i = 0; i < nodeCount; i++)
                adj[i] = new List<int>();

            for (var e = 0; e < edgeCount; e++)
            {
                var edge = edges[e];
                if (!idToIndex.TryGetValue(edge.fromNodeId, out var fromIdx)
                    || !idToIndex.TryGetValue(edge.toNodeId, out var toIdx))
                {
                    error = $"edge references unknown node ({edge.fromNodeId} → {edge.toNodeId}).";
                    return false;
                }

                if (edge.fromPort != VstGraphPort.Main)
                {
                    error = "V1 edges must use fromPort=Main.";
                    return false;
                }

                var toKind = nodeArr[toIdx].kind;
                if (edge.toPort == VstGraphPort.Sidechain)
                {
                    if (toKind != VstGraphNodeKind.Effect)
                    {
                        error = $"Sidechain edge only allowed into Effect (node {edge.toNodeId}).";
                        return false;
                    }

                    sideInCounts[toIdx]++;
                    if (sideInCounts[toIdx] > 1)
                    {
                        error = $"Effect node {edge.toNodeId} has more than one Sidechain input.";
                        return false;
                    }

                    sideSrc[toIdx] = fromIdx;
                    sideGain[toIdx] = edge.gain;
                }
                else
                {
                    mainInCounts[toIdx]++;
                    flatMainTo.Add(toIdx);
                    flatMainSrc.Add(fromIdx);
                    flatMainGain.Add(edge.gain);
                }

                // Topology edges: both Main and Sidechain create ordering constraints.
                adj[fromIdx].Add(toIdx);
                indegree[toIdx]++;
            }

            for (var i = 0; i < nodeCount; i++)
            {
                var kind = nodeArr[i].kind;
                var mainIn = mainInCounts[i];
                switch (kind)
                {
                    case VstGraphNodeKind.ExternalIn:
                        if (mainIn != 0 || sideInCounts[i] != 0)
                        {
                            error = "ExternalIn must not have inputs.";
                            return false;
                        }

                        break;
                    case VstGraphNodeKind.Instrument:
                        if (mainIn != 0 || sideInCounts[i] != 0)
                        {
                            error = $"Instrument node {nodeArr[i].id} must not have inputs.";
                            return false;
                        }

                        break;
                    case VstGraphNodeKind.Effect:
                        if (mainIn != 1)
                        {
                            error = $"Effect node {nodeArr[i].id} requires exactly 1 Main input (got {mainIn}).";
                            return false;
                        }

                        break;
                    case VstGraphNodeKind.Mix:
                        if (mainIn < 1)
                        {
                            error = $"Mix node {nodeArr[i].id} requires ≥1 Main input.";
                            return false;
                        }

                        if (sideInCounts[i] != 0)
                        {
                            error = $"Mix node {nodeArr[i].id} cannot have Sidechain inputs.";
                            return false;
                        }

                        break;
                    case VstGraphNodeKind.Split:
                    case VstGraphNodeKind.Gain:
                    case VstGraphNodeKind.Output:
                        if (mainIn != 1)
                        {
                            error = $"{kind} node {nodeArr[i].id} requires exactly 1 Main input (got {mainIn}).";
                            return false;
                        }

                        if (sideInCounts[i] != 0)
                        {
                            error = $"{kind} node {nodeArr[i].id} cannot have Sidechain inputs.";
                            return false;
                        }

                        break;
                }
            }

            // Kahn topological sort
            var queue = new Queue<int>();
            for (var i = 0; i < nodeCount; i++)
            {
                if (indegree[i] == 0)
                    queue.Enqueue(i);
            }

            var topo = new int[nodeCount];
            var topoCount = 0;
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                topo[topoCount++] = u;
                var outs = adj[u];
                for (var j = 0; j < outs.Count; j++)
                {
                    var v = outs[j];
                    indegree[v]--;
                    if (indegree[v] == 0)
                        queue.Enqueue(v);
                }
            }

            if (topoCount != nodeCount)
            {
                error = "graph contains a cycle (DAG required).";
                return false;
            }

            // Pack main inputs per node
            var mainStart = new int[nodeCount + 1];
            var mainSrcPacked = new int[flatMainSrc.Count];
            var mainGainPacked = new float[flatMainGain.Count];
            var write = 0;
            for (var i = 0; i < nodeCount; i++)
            {
                mainStart[i] = write;
                for (var e = 0; e < flatMainTo.Count; e++)
                {
                    if (flatMainTo[e] != i)
                        continue;
                    mainSrcPacked[write] = flatMainSrc[e];
                    mainGainPacked[write] = flatMainGain[e];
                    write++;
                }
            }

            mainStart[nodeCount] = write;

            var routeCount = channelRoutes != null ? channelRoutes.Count : 0;
            var routeArr = routeCount == 0 ? Array.Empty<ChannelRoute>() : channelRoutes.ToArray();

            var buf = snapWriteA ? snapA : snapB;
            if (buf == null)
                buf = new ArmedSnapshot();
            buf.nodes = nodeArr;
            buf.idToIndex = idToIndex;
            buf.topoOrder = topo;
            buf.mainInStart = mainStart;
            buf.mainInSrc = mainSrcPacked;
            buf.mainInGain = mainGainPacked;
            buf.sideInSrc = sideSrc;
            buf.sideInGain = sideGain;
            buf.outputIndex = outputIndex;
            buf.externalInIndex = externalIndex;
            buf.routes = routeArr;

            if (snapWriteA)
                snapA = buf;
            else
                snapB = buf;
            snapWriteA = !snapWriteA;

            snapshot = buf;
            return true;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (Volatile.Read(ref armed) == 0 || data == null || data.Length == 0 || channels <= 0)
                return;

            var host = VstHostManager.Instance;
            if (host == null || !host.IsInitialized)
            {
                warnedNotReady = true;
                return;
            }

            var frames = data.Length / channels;
            if (frames <= 0)
                return;
            if (frames > host.BlockSize)
            {
                VstHostAudioDiagnostics.RecordBlockSizeSkip(frames, host.BlockSize);
                return;
            }

            EnsureCapacity(frames);

            var sampleRate = host.SampleRate > 0 ? host.SampleRate : AudioSettings.outputSampleRate;
            if (sampleRate <= 0)
                sampleRate = 48000;
            var blockEnd = (long)(AudioSettings.dspTime * sampleRate) + frames;

            if (flushDspMidiQueue)
                VstHostDspMidiQueue.Shared.FlushDue(blockEnd);

            var snap = Volatile.Read(ref armedSnap);
            if (snap == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            if (snap.externalInIndex >= 0)
                Deinterleave(data, channels, frames, externalL, externalR);
            else
            {
                Array.Clear(externalL, 0, frames);
                Array.Clear(externalR, 0, frames);
            }

            ProcessArmed(host, snap, frames);
            InterleaveReplace(finalL, finalR, data, channels, frames, outputGain);
        }

        private void ProcessArmed(VstHostManager host, ArmedSnapshot snap, int frames)
        {
            Array.Clear(finalL, 0, frames);
            Array.Clear(finalR, 0, frames);

            for (var t = 0; t < snap.topoOrder.Length; t++)
            {
                var idx = snap.topoOrder[t];
                var node = snap.nodes[idx];
                var outL = nodeL[idx];
                var outR = nodeR[idx];

                switch (node.kind)
                {
                    case VstGraphNodeKind.ExternalIn:
                        CopyBuffer(externalL, externalR, outL, outR, frames);
                        break;

                    case VstGraphNodeKind.Instrument:
                        Array.Clear(outL, 0, frames);
                        Array.Clear(outR, 0, frames);
                        if (node.bypass || node.pluginId < 1)
                            break;
                        if (!host.Process(node.pluginId, null, null, outL, outR, frames))
                        {
                            VstHostAudioDiagnostics.RecordProcessFail();
                            Array.Clear(outL, 0, frames);
                            Array.Clear(outR, 0, frames);
                            break;
                        }

                        ScaleBuffer(outL, outR, frames, node.gain);
                        break;

                    case VstGraphNodeKind.Effect:
                        SumMainInputs(snap, idx, frames, tempInL, tempInR);
                        if (node.bypass || node.pluginId < 1)
                        {
                            CopyBuffer(tempInL, tempInR, outL, outR, frames);
                            ScaleBuffer(outL, outR, frames, node.gain);
                            break;
                        }

                        var processed = false;
                        var scSrc = snap.sideInSrc[idx];
                        if (scSrc >= 0)
                        {
                            CopyScaledBuffer(
                                nodeL[scSrc], nodeR[scSrc], tempScL, tempScR, frames, snap.sideInGain[idx]);
                            processed = host.ProcessWithSidechain(
                                node.pluginId,
                                tempInL, tempInR,
                                tempScL, tempScR,
                                tempOutL, tempOutR,
                                frames);
                        }
                        else
                        {
                            processed = host.Process(
                                node.pluginId, tempInL, tempInR, tempOutL, tempOutR, frames);
                        }

                        if (!processed)
                        {
                            VstHostAudioDiagnostics.RecordProcessFail();
                            CopyBuffer(tempInL, tempInR, outL, outR, frames);
                            break;
                        }

                        CopyBuffer(tempOutL, tempOutR, outL, outR, frames);
                        ScaleBuffer(outL, outR, frames, node.gain);
                        break;

                    case VstGraphNodeKind.Mix:
                        SumMainInputs(snap, idx, frames, outL, outR);
                        break;

                    case VstGraphNodeKind.Split:
                        SumMainInputs(snap, idx, frames, outL, outR);
                        break;

                    case VstGraphNodeKind.Gain:
                        SumMainInputs(snap, idx, frames, outL, outR);
                        if (!node.bypass)
                            ScaleBuffer(outL, outR, frames, node.gain);
                        break;

                    case VstGraphNodeKind.Output:
                        SumMainInputs(snap, idx, frames, finalL, finalR);
                        break;
                }
            }
        }

        private void SumMainInputs(ArmedSnapshot snap, int nodeIndex, int frames, float[] dstL, float[] dstR)
        {
            Array.Clear(dstL, 0, frames);
            Array.Clear(dstR, 0, frames);
            var start = snap.mainInStart[nodeIndex];
            var end = snap.mainInStart[nodeIndex + 1];
            for (var e = start; e < end; e++)
            {
                var src = snap.mainInSrc[e];
                var g = snap.mainInGain[e];
                AddScaledBuffer(nodeL[src], nodeR[src], dstL, dstR, frames, g);
            }
        }

        private static void Deinterleave(float[] interleaved, int channels, int frames, float[] left, float[] right)
        {
            if (channels == 1)
            {
                for (var i = 0; i < frames; i++)
                {
                    left[i] = interleaved[i];
                    right[i] = interleaved[i];
                }

                return;
            }

            for (var i = 0; i < frames; i++)
            {
                var baseIndex = i * channels;
                left[i] = interleaved[baseIndex];
                right[i] = interleaved[baseIndex + 1];
            }
        }

        private static void CopyScaledBuffer(
            float[] srcL, float[] srcR, float[] dstL, float[] dstR, int frames, float gain)
        {
            if (Mathf.Approximately(gain, 1f))
            {
                CopyBuffer(srcL, srcR, dstL, dstR, frames);
                return;
            }

            for (var i = 0; i < frames; i++)
            {
                dstL[i] = srcL[i] * gain;
                dstR[i] = srcR[i] * gain;
            }
        }

        private static void ScaleBuffer(float[] l, float[] r, int frames, float gain)
        {
            if (Mathf.Approximately(gain, 1f))
                return;
            for (var i = 0; i < frames; i++)
            {
                l[i] *= gain;
                r[i] *= gain;
            }
        }

        private static void AddScaledBuffer(float[] srcL, float[] srcR, float[] dstL, float[] dstR, int frames, float gain)
        {
            if (Mathf.Approximately(gain, 1f))
            {
                for (var i = 0; i < frames; i++)
                {
                    dstL[i] += srcL[i];
                    dstR[i] += srcR[i];
                }

                return;
            }

            for (var i = 0; i < frames; i++)
            {
                dstL[i] += srcL[i] * gain;
                dstR[i] += srcR[i] * gain;
            }
        }

        private static void CopyBuffer(float[] srcL, float[] srcR, float[] dstL, float[] dstR, int frames)
        {
            Array.Copy(srcL, 0, dstL, 0, frames);
            Array.Copy(srcR, 0, dstR, 0, frames);
        }

        private static void InterleaveReplace(float[] left, float[] right, float[] interleaved, int channels, int frames, float gain)
        {
            if (channels == 1)
            {
                for (var i = 0; i < frames; i++)
                    interleaved[i] = 0.5f * (left[i] + right[i]) * gain;
                return;
            }

            for (var i = 0; i < frames; i++)
            {
                var baseIndex = i * channels;
                interleaved[baseIndex] = left[i] * gain;
                interleaved[baseIndex + 1] = right[i] * gain;
                for (var c = 2; c < channels; c++)
                    interleaved[baseIndex + c] = 0f;
            }
        }

        private sealed class ArmedSnapshot
        {
            public VstGraphNode[] nodes;
            public Dictionary<int, int> idToIndex;
            public int[] topoOrder;
            public int[] mainInStart;
            public int[] mainInSrc;
            public float[] mainInGain;
            public int[] sideInSrc;
            public float[] sideInGain;
            public int outputIndex;
            public int externalInIndex;
            public ChannelRoute[] routes;

            public bool TryGetNodeIndex(int nodeId, out int index) =>
                idToIndex != null && idToIndex.TryGetValue(nodeId, out index);
        }
    }
}
