#nullable enable
using System.ComponentModel;
using System.IO;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using jp.kshoji.unity.vst3nativehost.timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace jp.kshoji.unity.vst3nativehost.mcp.timeline
{
    [AiToolType]
    public class Tool_VstTimeline
    {
        const string DefaultObjectName = "__VstHostMcpTimeline";

        [AiTool("vst3-timeline-param-track", Title = "VST3 / Timeline Parameter Track")]
        [Description(
            "Editor only — ensure PlayableDirector + TimelineAsset with a VstParameterTrack bound to VstParameterTarget, " +
            "and a clip for parameterId. Creates Assets/... timeline if needed. " +
            "Standalone: use vst3-timeline-director-control on a pre-authored scene.")]
        public string TimelineParamTrack
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("VST parameter id for the clip.")]
            long parameterId,
            [Description("Timeline asset path Assets/... .playable")]
            string timelineAssetPath = "Assets/VstMcpTimelines/Param.playable",
            [Description("Clip duration seconds.")]
            double clipDuration = 2.0,
            [Description("Normalized start value.")]
            float startValue = 0f,
            [Description("Normalized end value.")]
            float endValue = 1f,
            [Description("GameObject for director/target. Empty = __VstHostMcpTimeline.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (pluginId < 1)
                    return "[Error] vst3-timeline-param-track: pluginId must be >= 1.";

                var path = NormalizePlayablePath(timelineAssetPath);
                EnsureAssetFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));

                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                if (timeline == null)
                {
                    timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                    AssetDatabase.CreateAsset(timeline, path);
                }

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name) ?? new GameObject(name);
                if (go.hideFlags == HideFlags.None)
                    go.hideFlags = HideFlags.DontSave;

                var target = go.GetComponent<VstParameterTarget>() ?? go.AddComponent<VstParameterTarget>();
                target.PluginId = pluginId;

                var director = go.GetComponent<PlayableDirector>() ?? go.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;

                VstParameterTrack? track = null;
                foreach (var t in timeline.GetOutputTracks())
                {
                    if (t is VstParameterTrack vt)
                    {
                        track = vt;
                        break;
                    }
                }

                if (track == null)
                    track = timeline.CreateTrack<VstParameterTrack>(null, "VST Parameter");

                director.SetGenericBinding(track, target);

                TimelineClip? clip = null;
                foreach (var c in track.GetClips())
                {
                    if (c.asset is VstParameterClip)
                    {
                        clip = c;
                        break;
                    }
                }

                if (clip == null)
                    clip = track.CreateClip<VstParameterClip>();

                clip.start = 0;
                clip.duration = System.Math.Max(0.1, clipDuration);
                var asset = (VstParameterClip)clip.asset;
                asset.parameterId = unchecked((uint)parameterId);
                asset.curve = AnimationCurve.Linear(0f, startValue, 1f, endValue);

                EditorUtility.SetDirty(asset);
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();

                return
                    $"[Success] vst3-timeline-param-track timeline={path} gameObject={go.name} " +
                    $"pluginId={pluginId} paramId={parameterId} duration={clip.duration}";
            });
        }

        [AiTool("vst3-timeline-program-marker", Title = "VST3 / Timeline Program Marker")]
        [Description(
            "Editor only — ensure VstTimelineNotificationReceiver and a VstProgramChangeMarker on a marker track.")]
        public string TimelineProgramMarker
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Host program index.")]
            int programIndex = 0,
            [Description("Marker time in seconds.")]
            double time = 0.5,
            [Description("Timeline asset path.")]
            string timelineAssetPath = "Assets/VstMcpTimelines/Param.playable",
            [Description("GameObject name. Empty = __VstHostMcpTimeline.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var path = NormalizePlayablePath(timelineAssetPath);
                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                if (timeline == null)
                    return $"[Error] timeline not found at {path}. Call vst3-timeline-param-track first.";

                var name = string.IsNullOrWhiteSpace(gameObjectName)
                    ? DefaultObjectName
                    : gameObjectName.Trim();
                var go = GameObject.Find(name);
                if (go == null)
                    return $"[Error] GameObject '{name}' not found.";

                var target = go.GetComponent<VstParameterTarget>() ?? go.AddComponent<VstParameterTarget>();
                target.PluginId = pluginId;

                var receiver = go.GetComponent<VstTimelineNotificationReceiver>()
                               ?? go.AddComponent<VstTimelineNotificationReceiver>();
                receiver.DefaultTarget = target;

                MarkerTrack? markerTrack = null;
                foreach (var t in timeline.GetOutputTracks())
                {
                    if (t is MarkerTrack mt)
                    {
                        markerTrack = mt;
                        break;
                    }
                }

                if (markerTrack == null)
                    markerTrack = timeline.CreateTrack<MarkerTrack>(null, "VST Markers");

                var marker = markerTrack.CreateMarker<VstProgramChangeMarker>(time);
                marker.programIndex = programIndex;
                marker.target = target;

                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();

                return
                    $"[Success] vst3-timeline-program-marker timeline={path} time={time} " +
                    $"programIndex={programIndex} pluginId={pluginId}";
            });
        }

        static string NormalizePlayablePath(string? assetPath)
        {
            var path = (assetPath ?? "Assets/VstMcpTimelines/Param.playable").Trim().Replace('\\', '/');
            if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                path = "Assets/" + path.TrimStart('/');
            if (!path.EndsWith(".playable", System.StringComparison.OrdinalIgnoreCase))
                path += ".playable";
            return path;
        }

        static void EnsureAssetFolder(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return;
            var parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;
            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
