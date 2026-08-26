#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.midi;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.midi
{
    /// <summary>Runtime MCP tools for MIDI Plugin ↔ VST wiring (no asset-path CRUD).</summary>
    [AiToolType]
    public class Tool_VstMidi
    {
        internal const string DefaultObjectName = VstHostToolHelpers.DefaultMidiObjectName;

        internal static GameObject FindOrCreate(string? gameObjectName, bool dontSave = true)
        {
            var name = string.IsNullOrWhiteSpace(gameObjectName)
                ? DefaultObjectName
                : gameObjectName.Trim();
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                if (dontSave)
                    go.hideFlags = HideFlags.DontSave;
            }

            return go;
        }

        internal static string FormatAdapter(VstHostMidiAdapter adapter)
        {
            var sb = new StringBuilder();
            sb.Append($"targetPluginId={adapter.TargetPluginId}");
            sb.Append($" registered={adapter.IsRegistered}");
            sb.Append($" forwardCC={adapter.ForwardControlChange}");
            sb.Append($" forwardPB={adapter.ForwardPitchBend}");
            sb.Append($" forwardPC={adapter.ForwardProgramChange}");
            sb.Append($" mapPCToHostProgram={adapter.MapProgramChangeToHostProgram}");
            var routes = adapter.ChannelRoutes;
            sb.Append($" channelRouteCount={routes?.Count ?? 0}");
            if (routes != null)
            {
                for (var i = 0; i < routes.Count; i++)
                {
                    var r = routes[i];
                    sb.Append($"\n  ch={r.channel}→pluginId={r.pluginId}");
                }
            }

            return sb.ToString();
        }

        internal static bool TryParseChannelRoutes(
            string routesText,
            out List<VstHostMidiAdapter.ChannelRoute> routes,
            out string? error)
        {
            routes = new List<VstHostMidiAdapter.ChannelRoute>();
            error = null;
            if (string.IsNullOrWhiteSpace(routesText))
                return true;

            var parts = routesText.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in parts)
            {
                var token = raw.Trim();
                if (token.Length == 0)
                    continue;
                var sep = token.IndexOf(':');
                if (sep < 0)
                    sep = token.IndexOf('=');
                if (sep < 0)
                {
                    error = $"[Error] channel route '{token}' must be channel:pluginId (e.g. 0:1,1:2).";
                    return false;
                }

                if (!int.TryParse(token.Substring(0, sep).Trim(), out var ch)
                    || !int.TryParse(token.Substring(sep + 1).Trim(), out var pluginId))
                {
                    error = $"[Error] channel route '{token}' is not channel:pluginId integers.";
                    return false;
                }

                if (ch < 0 || ch > 15)
                {
                    error = $"[Error] channel must be 0–15 (got {ch}).";
                    return false;
                }

                if (pluginId < 1)
                {
                    error = $"[Error] pluginId must be >= 1 (got {pluginId}).";
                    return false;
                }

                routes.Add(new VstHostMidiAdapter.ChannelRoute { channel = ch, pluginId = pluginId });
            }

            return true;
        }

        internal static string[]? ParseDeviceIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return null;
            return csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
        }

        [AiTool("vst3-midi-adapter-setup", Title = "VST3 / MIDI Adapter Setup")]
        [Description(
            "Ensure GameObject has VstHostMidiAdapter: TargetPluginId, forward CC/PB/PC, " +
            "MapProgramChangeToHostProgram, optional device allow-list, Register(). " +
            "Creates __VstHostMcpMidi when gameObjectName is empty. Play Mode / runtime + MidiManager.")]
        public string MidiAdapterSetup
        (
            [Description("Loaded VST plugin instance id receiving MIDI.")]
            int targetPluginId,
            [Description("Forward Control Change to host.")]
            bool forwardControlChange = true,
            [Description("Forward Pitch Bend to host.")]
            bool forwardPitchBend = true,
            [Description("Forward Program Change as MIDI to host.")]
            bool forwardProgramChange = true,
            [Description("Also call VstHostManager.SetProgram on Program Change.")]
            bool mapProgramChangeToHostProgram = false,
            [Description("Comma-separated allowed device ids. Empty = no device filter.")]
            string? allowedDeviceIds = null,
            [Description("Target GameObject name. Empty = __VstHostMcpMidi.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (targetPluginId < 1)
                    return "[Error] vst3-midi-adapter-setup: targetPluginId must be >= 1.";

                var go = FindOrCreate(gameObjectName);
                var adapter = go.GetComponent<VstHostMidiAdapter>();
                if (adapter == null)
                    adapter = go.AddComponent<VstHostMidiAdapter>();

                adapter.TargetPluginId = targetPluginId;
                adapter.ForwardControlChange = forwardControlChange;
                adapter.ForwardPitchBend = forwardPitchBend;
                adapter.ForwardProgramChange = forwardProgramChange;
                adapter.MapProgramChangeToHostProgram = mapProgramChangeToHostProgram;

                var devices = ParseDeviceIds(allowedDeviceIds);
                if (devices != null && devices.Length > 0)
                    adapter.SetAllowedDeviceIds(devices);

                if (!adapter.IsRegistered)
                    adapter.Register();

                var midiOk = MidiManager.Instance != null;
                return
                    $"[Success] vst3-midi-adapter-setup gameObject={go.name} midiManager={midiOk} " +
                    FormatAdapter(adapter);
            });
        }

        [AiTool("vst3-smf-link", Title = "VST3 / SMF Link")]
        [Description(
            "Ensure VstHostSmfLink: register virtual device (default vst3:smf), set SmfPlayer.outputDeviceId, " +
            "optionally filter Adapter/Mapper to that device. Finds/adds SmfPlayer + Adapter on same GO.")]
        public string SmfLink
        (
            [Description("Virtual device id (default vst3:smf).")]
            string? virtualDeviceId = null,
            [Description("Connect now (register + apply).")]
            bool connect = true,
            [Description("Target GameObject name. Empty = __VstHostMcpMidi.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var go = FindOrCreate(gameObjectName);
                if (go.GetComponent<VstHostMidiAdapter>() == null)
                    go.AddComponent<VstHostMidiAdapter>();
                if (go.GetComponent<SmfPlayer>() == null)
                    go.AddComponent<SmfPlayer>();

                var link = go.GetComponent<VstHostSmfLink>();
                if (link == null)
                    link = go.AddComponent<VstHostSmfLink>();

                if (!string.IsNullOrWhiteSpace(virtualDeviceId))
                    link.VirtualDeviceId = virtualDeviceId.Trim();

                link.SmfPlayer = go.GetComponent<SmfPlayer>();
                link.MidiAdapter = go.GetComponent<VstHostMidiAdapter>();

                if (connect)
                    link.Connect();

                return
                    $"[Success] vst3-smf-link gameObject={go.name} virtualDeviceId={link.VirtualDeviceId} " +
                    $"registered={link.IsRegistered} smfOutput={link.SmfPlayer?.outputDeviceId} " +
                    $"midiManager={(MidiManager.Instance != null)}";
            });
        }

        [AiTool("vst3-channel-routes", Title = "VST3 / Channel Routes")]
        [Description(
            "Set multi-timbral channel→pluginId routes on VstHostMidiAdapter. " +
            "routes format: '0:1,1:2' (channel:pluginId). Empty clears routes (TargetPluginId only).")]
        public string ChannelRoutes
        (
            [Description("Routes string e.g. 0:1,1:2. Empty clears.")]
            string? routes = null,
            [Description("GameObject with Adapter. Empty = __VstHostMcpMidi.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryParseChannelRoutes(routes ?? string.Empty, out var list, out var parseErr))
                    return parseErr!;

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                    return $"[Error] vst3-channel-routes: GameObject '{name}' not found. Call vst3-midi-adapter-setup first.";

                var adapter = go.GetComponent<VstHostMidiAdapter>();
                if (adapter == null)
                    return $"[Error] vst3-channel-routes: VstHostMidiAdapter missing on '{name}'.";

                adapter.SetChannelRoutes(list);
                return $"[Success] vst3-channel-routes gameObject={name}\n{FormatAdapter(adapter)}";
            });
        }

        [AiTool("vst3-filter-link", Title = "VST3 / MIDI Filter Link")]
        [Description(
            "Ensure VstHostMidiFilterLink: wire MidiChannelFilter.forwardTargets → Adapter and " +
            "optionally Unregister Adapter from MidiManager (Filter → Adapter only).")]
        public string FilterLink
        (
            [Description("Unregister Adapter from MidiManager after wiring (default true).")]
            bool unregisterAdapterFromMidiManager = true,
            [Description("Apply immediately.")]
            bool apply = true,
            [Description("GameObject name. Empty = __VstHostMcpMidi.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var go = FindOrCreate(gameObjectName);
                if (go.GetComponent<VstHostMidiAdapter>() == null)
                    go.AddComponent<VstHostMidiAdapter>();

                var filter = go.GetComponent<MidiChannelFilter>();
                if (filter == null)
                    filter = go.AddComponent<MidiChannelFilter>();

                var link = go.GetComponent<VstHostMidiFilterLink>();
                if (link == null)
                    link = go.AddComponent<VstHostMidiFilterLink>();

                var adapter = go.GetComponent<VstHostMidiAdapter>();
                link.Configure(filter, adapter, unregisterAdapterFromMidiManager);

                if (apply)
                    link.Apply();

                return
                    $"[Success] vst3-filter-link gameObject={go.name} " +
                    $"adapterRegistered={adapter?.IsRegistered} " +
                    $"unregisterAdapter={unregisterAdapterFromMidiManager}";
            });
        }

        [AiTool("vst3-route-sync", Title = "VST3 / Channel Route Sync")]
        [Description(
            "Ensure VstHostChannelRouteSync and run Sync between Adapter and VstAudioGraph. " +
            "direction: AdapterToGraph | GraphToAdapter | BidirectionalOnEnable.")]
        public string RouteSync
        (
            [Description("Sync direction.")]
            string direction = "GraphToAdapter",
            [Description("Run Sync() now.")]
            bool syncNow = true,
            [Description("GameObject name. Empty = __VstHostMcpMidi.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryParseSyncDirection(direction, out var dir, out var dirErr))
                    return dirErr!;

                var go = FindOrCreate(gameObjectName);
                if (go.GetComponent<VstHostMidiAdapter>() == null)
                    go.AddComponent<VstHostMidiAdapter>();
                if (go.GetComponent<VstAudioGraph>() == null)
                    go.AddComponent<VstAudioGraph>();

                var sync = go.GetComponent<VstHostChannelRouteSync>();
                if (sync == null)
                    sync = go.AddComponent<VstHostChannelRouteSync>();

                sync.Configure(
                    go.GetComponent<VstHostMidiAdapter>(),
                    go.GetComponent<VstAudioGraph>(),
                    dir);

                if (syncNow)
                    sync.Sync();

                var adapter = go.GetComponent<VstHostMidiAdapter>();
                return
                    $"[Success] vst3-route-sync gameObject={go.name} direction={dir} synced={syncNow}\n" +
                    FormatAdapter(adapter!);
            });
        }

        static bool TryParseSyncDirection(
            string text,
            out VstHostChannelRouteSync.SyncDirection direction,
            out string? error)
        {
            direction = VstHostChannelRouteSync.SyncDirection.GraphToAdapter;
            error = null;
            if (string.IsNullOrWhiteSpace(text)
                || text.Equals("GraphToAdapter", StringComparison.OrdinalIgnoreCase))
            {
                direction = VstHostChannelRouteSync.SyncDirection.GraphToAdapter;
                return true;
            }

            if (text.Equals("AdapterToGraph", StringComparison.OrdinalIgnoreCase))
            {
                direction = VstHostChannelRouteSync.SyncDirection.AdapterToGraph;
                return true;
            }

            if (text.Equals("BidirectionalOnEnable", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Bidirectional", StringComparison.OrdinalIgnoreCase))
            {
                direction = VstHostChannelRouteSync.SyncDirection.BidirectionalOnEnable;
                return true;
            }

            error =
                "[Error] vst3-route-sync: direction must be GraphToAdapter, AdapterToGraph, " +
                "or BidirectionalOnEnable.";
            return false;
        }
    }
}
