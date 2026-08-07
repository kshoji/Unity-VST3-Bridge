#if FEATURE_MIDI_PLUGIN
using System.Collections.Generic;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Keeps <see cref="VstHostMidiAdapter"/> channel→plugin routes aligned with
    /// <see cref="VstAudioGraph"/> channel→instrument node routes (and vice versa).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostChannelRouteSync : MonoBehaviour
    {
        public enum SyncDirection
        {
            AdapterToGraph = 0,
            GraphToAdapter = 1,
            BidirectionalOnEnable = 2,
        }

        [SerializeField] private VstHostMidiAdapter adapter;
        [SerializeField] private VstAudioGraph graph;
        [SerializeField] private SyncDirection direction = SyncDirection.GraphToAdapter;
        [SerializeField] private bool syncOnEnable = true;

        private void Reset()
        {
            adapter = GetComponent<VstHostMidiAdapter>();
            graph = GetComponent<VstAudioGraph>();
        }

        private void OnEnable()
        {
            if (syncOnEnable)
                Sync();
        }

        /// <summary>Applies the configured sync direction once.</summary>
        public void Sync()
        {
            if (adapter == null)
                adapter = GetComponent<VstHostMidiAdapter>();
            if (graph == null)
                graph = GetComponent<VstAudioGraph>();
            if (adapter == null || graph == null)
            {
                Debug.LogWarning("[VstHostChannelRouteSync] Adapter and VstAudioGraph are required.", this);
                return;
            }

            switch (direction)
            {
                case SyncDirection.AdapterToGraph:
                    ApplyAdapterToGraph();
                    break;
                case SyncDirection.GraphToAdapter:
                    ApplyGraphToAdapter();
                    break;
                case SyncDirection.BidirectionalOnEnable:
                    if (graph.ChannelRoutes != null && graph.ChannelRoutes.Count > 0)
                        ApplyGraphToAdapter();
                    else
                        ApplyAdapterToGraph();
                    break;
            }
        }

        public void ApplyGraphToAdapter()
        {
            if (adapter == null || graph == null)
                return;

            var routes = new List<VstHostMidiAdapter.ChannelRoute>();
            var nodes = graph.Nodes;
            var channelRoutes = graph.ChannelRoutes;
            if (channelRoutes != null && nodes != null)
            {
                for (var i = 0; i < channelRoutes.Count; i++)
                {
                    var cr = channelRoutes[i];
                    if (!TryFindInstrumentPluginId(nodes, cr.nodeId, out var pluginId))
                        continue;
                    routes.Add(new VstHostMidiAdapter.ChannelRoute
                    {
                        channel = cr.channel,
                        pluginId = pluginId,
                    });
                }
            }

            adapter.SetChannelRoutes(routes);
        }

        public void ApplyAdapterToGraph()
        {
            if (adapter == null || graph == null)
                return;

            var nodes = graph.Nodes;
            if (nodes == null || nodes.Count == 0)
                return;

            var pluginToNodeId = new Dictionary<int, int>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.kind != VstGraphNodeKind.Instrument || n.pluginId < 1)
                    continue;
                if (!pluginToNodeId.ContainsKey(n.pluginId))
                    pluginToNodeId[n.pluginId] = n.id;
            }

            var built = new List<VstAudioGraph.ChannelRoute>();
            var adapterRoutes = adapter.ChannelRoutes;
            if (adapterRoutes != null)
            {
                for (var i = 0; i < adapterRoutes.Count; i++)
                {
                    var ar = adapterRoutes[i];
                    if (ar.pluginId < 1 || !pluginToNodeId.TryGetValue(ar.pluginId, out var nodeId))
                        continue;
                    built.Add(new VstAudioGraph.ChannelRoute
                    {
                        channel = ar.channel,
                        nodeId = nodeId,
                    });
                }
            }

            graph.SetChannelRoutes(built);
        }

        private static bool TryFindInstrumentPluginId(
            IList<VstGraphNode> nodes, int nodeId, out int pluginId)
        {
            pluginId = -1;
            if (nodes == null)
                return false;
            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.id != nodeId)
                    continue;
                if (n.kind != VstGraphNodeKind.Instrument || n.pluginId < 1)
                    return false;
                pluginId = n.pluginId;
                return true;
            }

            return false;
        }
    }
}
#endif
