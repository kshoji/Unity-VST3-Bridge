#if FEATURE_MIDI_PLUGIN
using System.Collections.Generic;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    /// <summary>
    /// Keeps <see cref="VstHostMidiAdapter"/> channel→plugin routes aligned with
    /// <see cref="VstPluginChain"/> channel→slot routes (and vice versa).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VstHostChannelRouteSync : MonoBehaviour
    {
        public enum SyncDirection
        {
            AdapterToChain = 0,
            ChainToAdapter = 1,
            BidirectionalOnEnable = 2,
        }

        [SerializeField] private VstHostMidiAdapter adapter;
        [SerializeField] private VstPluginChain chain;
        [SerializeField] private SyncDirection direction = SyncDirection.ChainToAdapter;
        [SerializeField] private bool syncOnEnable = true;

        private void Reset()
        {
            adapter = GetComponent<VstHostMidiAdapter>();
            chain = GetComponent<VstPluginChain>();
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
            if (chain == null)
                chain = GetComponent<VstPluginChain>();
            if (adapter == null || chain == null)
            {
                Debug.LogWarning("[VstHostChannelRouteSync] Adapter and VstPluginChain are required.", this);
                return;
            }

            switch (direction)
            {
                case SyncDirection.AdapterToChain:
                    ApplyAdapterToChain();
                    break;
                case SyncDirection.ChainToAdapter:
                    ApplyChainToAdapter();
                    break;
                case SyncDirection.BidirectionalOnEnable:
                    if (chain.ChannelRoutes != null && chain.ChannelRoutes.Count > 0)
                        ApplyChainToAdapter();
                    else
                        ApplyAdapterToChain();
                    break;
            }
        }

        public void ApplyChainToAdapter()
        {
            if (adapter == null || chain == null)
                return;

            var routes = new List<VstHostMidiAdapter.ChannelRoute>();
            var slots = chain.Slots;
            var channelRoutes = chain.ChannelRoutes;
            if (channelRoutes != null && slots != null)
            {
                for (var i = 0; i < channelRoutes.Count; i++)
                {
                    var cr = channelRoutes[i];
                    if (cr.slotIndex < 0 || cr.slotIndex >= slots.Count)
                        continue;
                    var slot = slots[cr.slotIndex];
                    if (slot.role != VstPluginChain.SlotRole.Instrument || slot.pluginId < 1)
                        continue;
                    routes.Add(new VstHostMidiAdapter.ChannelRoute
                    {
                        channel = cr.channel,
                        pluginId = slot.pluginId,
                    });
                }
            }

            adapter.SetChannelRoutes(routes);
        }

        public void ApplyAdapterToChain()
        {
            if (adapter == null || chain == null)
                return;

            var slots = chain.Slots;
            if (slots == null || slots.Count == 0)
                return;

            var pluginToSlot = new Dictionary<int, int>();
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].role != VstPluginChain.SlotRole.Instrument || slots[i].pluginId < 1)
                    continue;
                if (!pluginToSlot.ContainsKey(slots[i].pluginId))
                    pluginToSlot[slots[i].pluginId] = i;
            }

            var chainRoutes = chain.ChannelRoutes;
            chainRoutes.Clear();

            var adapterRoutes = adapter.ChannelRoutes;
            if (adapterRoutes == null)
                return;

            for (var i = 0; i < adapterRoutes.Count; i++)
            {
                var ar = adapterRoutes[i];
                if (ar.pluginId < 1 || !pluginToSlot.TryGetValue(ar.pluginId, out var slotIndex))
                    continue;
                chainRoutes.Add(new VstPluginChain.ChannelRoute
                {
                    channel = ar.channel,
                    slotIndex = slotIndex,
                });
            }
        }
    }
}
#endif
