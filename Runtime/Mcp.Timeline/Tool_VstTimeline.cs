#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.mcp.core;
using UnityEngine;
using UnityEngine.Playables;

namespace jp.kshoji.unity.vst3nativehost.mcp.timeline
{
    /// <summary>Runtime MCP control for scene PlayableDirector (no TimelineAsset creation).</summary>
    [AiToolType]
    public class Tool_VstTimeline
    {
        internal const string DefaultObjectName = "__VstHostMcpTimeline";

        [AiTool(
            "vst3-timeline-director-control",
            Title = "VST3 / Timeline Director Control",
            ReadOnlyHint = false)]
        [Description(
            "Control a scene PlayableDirector with an existing TimelineAsset. " +
            "action: status | play | pause | stop | set-time. " +
            "Use Editor-only vst3-timeline-param-track to create assets.")]
        public string TimelineDirectorControl
        (
            [Description("status, play, pause, stop, or set-time.")]
            string action = "status",
            [Description("GameObject with PlayableDirector. Empty = __VstHostMcpTimeline.")]
            string? gameObjectName = null,
            [Description("Timeline time in seconds (set-time).")]
            double? time = null,
            [Description("Evaluate graph after set-time (default true).")]
            bool evaluateAfterSetTime = true
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!VstHostToolHelpers.TryRequireAudioSession(
                        "vst3-timeline-director-control", out var sessionErr))
                    return sessionErr!;

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                    return $"[Error] vst3-timeline-director-control: GameObject '{name}' not found.";

                var director = go.GetComponent<PlayableDirector>();
                if (director == null)
                    return $"[Error] vst3-timeline-director-control: PlayableDirector missing on '{name}'.";

                var act = (action ?? "status").Trim().ToLowerInvariant();
                switch (act)
                {
                    case "status":
                    case "get":
                        return FormatStatus(name, director);
                    case "play":
                        director.Play();
                        return $"[Success] vst3-timeline-director-control play gameObject={name}\n{FormatStatus(name, director)}";
                    case "pause":
                        director.Pause();
                        return $"[Success] vst3-timeline-director-control pause gameObject={name}\n{FormatStatus(name, director)}";
                    case "stop":
                        director.Stop();
                        return $"[Success] vst3-timeline-director-control stop gameObject={name}\n{FormatStatus(name, director)}";
                    case "set-time":
                    case "settime":
                    case "time":
                        if (!time.HasValue)
                            return "[Error] vst3-timeline-director-control: time is required for set-time.";
                        director.time = Math.Max(0, time.Value);
                        if (evaluateAfterSetTime)
                            director.Evaluate();
                        return
                            $"[Success] vst3-timeline-director-control set-time={director.time:0.###} " +
                            $"gameObject={name}\n{FormatStatus(name, director)}";
                    default:
                        return "[Error] vst3-timeline-director-control: action must be status|play|pause|stop|set-time.";
                }
            });
        }

        static string FormatStatus(string gameObjectName, PlayableDirector director)
        {
            var assetName = director.playableAsset != null ? director.playableAsset.name : "(none)";
            var duration = director.playableAsset != null ? director.duration : 0;
            var target = director.GetComponent<VstParameterTarget>();
            var targetMsg = target != null ? $" targetPluginId={target.PluginId}" : string.Empty;
            return
                $"[Success] vst3-timeline-director-control status gameObject={gameObjectName} " +
                $"state={director.state} time={director.time:0.###} duration={duration:0.###} " +
                $"asset={assetName}{targetMsg}";
        }
    }
}
