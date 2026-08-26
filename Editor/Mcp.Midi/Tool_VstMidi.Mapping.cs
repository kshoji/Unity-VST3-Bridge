#nullable enable
using System.ComponentModel;
using System.IO;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using jp.kshoji.unity.vst3nativehost;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.midi
{
    public partial class Tool_VstMidi
    {
        [AiTool("vst3-cc-mapping-create", Title = "VST3 / CC Mapping Create")]
        [Description(
            "Editor only — creates a VstMidiParameterMapping asset under Assets/... .asset. " +
            "Standalone builds use scene/Resources mappings wired in Editor. " +
            "Optionally assign it to VstHostMidiParameterMapper on a GameObject.")]
        public string CcMappingCreate
        (
            [Description("Asset path e.g. Assets/VstMcpMappings/Learn.asset")]
            string assetPath,
            [Description("If set, ensure Mapper on this GO and assign the new mapping.")]
            string? assignToGameObject = null,
            [Description("Mapper TargetPluginId when assigning (required if assignToGameObject set).")]
            int targetPluginId = 0
        )
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return "[Error] vst3-cc-mapping-create: assetPath is required.";

            return MainThread.Instance.Run(() =>
            {
                var path = assetPath.Trim().Replace('\\', '/');
                if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                    return "[Error] vst3-cc-mapping-create: path must start with Assets/.";
                if (!path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                    path += ".asset";

                var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                    EnsureAssetFolder(dir!);

                if (AssetDatabase.LoadAssetAtPath<VstMidiParameterMapping>(path) != null)
                    return $"[Error] vst3-cc-mapping-create: asset already exists at {path}";

                var mapping = ScriptableObject.CreateInstance<VstMidiParameterMapping>();
                AssetDatabase.CreateAsset(mapping, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var assignMsg = string.Empty;
                if (!string.IsNullOrWhiteSpace(assignToGameObject))
                {
                    if (targetPluginId < 1)
                    {
                        return
                            $"[Success] created {path} but assign skipped: targetPluginId required when " +
                            "assignToGameObject is set.";
                    }

                    assignMsg = AssignMappingToMapper(assignToGameObject.Trim(), mapping, targetPluginId);
                }

                return $"[Success] vst3-cc-mapping-create path={path}{assignMsg}";
            });
        }

        [AiTool(
            "vst3-cc-mapping-list",
            Title = "VST3 / CC Mapping List",
            ReadOnlyHint = true,
            IdempotentHint = true)]
        [Description("Editor only — list VstMidiParameterMapping assets in the project (optional path filter).")]
        public string CcMappingList
        (
            [Description("Optional Assets/ folder or path substring filter.")]
            string? pathFilter = null,
            [Description("Max assets to list.")]
            int max = 50
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var guids = AssetDatabase.FindAssets("t:VstMidiParameterMapping");
                var sb = new StringBuilder();
                sb.Append($"[Success] vst3-cc-mapping-list found={guids.Length}");
                var shown = 0;
                var filter = string.IsNullOrWhiteSpace(pathFilter) ? null : pathFilter.Trim().Replace('\\', '/');
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (filter != null
                        && path.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (shown >= max)
                    {
                        sb.Append($"\n… truncated at max={max}");
                        break;
                    }

                    var asset = AssetDatabase.LoadAssetAtPath<VstMidiParameterMapping>(path);
                    var n = asset?.Bindings?.Length ?? 0;
                    sb.Append($"\n[{shown}] path={path} bindings={n}");
                    shown++;
                }

                return sb.ToString();
            });
        }

        [AiTool("vst3-cc-mapping-edit", Title = "VST3 / CC Mapping Edit")]
        [Description(
            "Editor only — upsert a binding on a VstMidiParameterMapping asset. " +
            "source: ControlChange | FourteenBitControlChange | PitchBend.")]
        public string CcMappingEdit
        (
            [Description("Asset path to VstMidiParameterMapping.")]
            string assetPath,
            [Description("VST parameter id.")]
            long parameterId,
            [Description("ControlChange, FourteenBitControlChange, or PitchBend.")]
            string source = "ControlChange",
            [Description("MIDI channel 0–15, or -1 for all.")]
            int channel = -1,
            [Description("CC number (MSB for 14-bit). Ignored for PitchBend.")]
            int controller = 1,
            [Description("LSB CC for 14-bit (0 = auto MSB+32).")]
            int controllerLsb = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var path = assetPath?.Trim().Replace('\\', '/') ?? string.Empty;
                var mapping = AssetDatabase.LoadAssetAtPath<VstMidiParameterMapping>(path);
                if (mapping == null)
                    return $"[Error] vst3-cc-mapping-edit: no VstMidiParameterMapping at '{path}'.";

                var paramId = unchecked((uint)parameterId);
                if (source.Equals("PitchBend", System.StringComparison.OrdinalIgnoreCase)
                    || source.Equals("PB", System.StringComparison.OrdinalIgnoreCase))
                {
                    mapping.UpsertPitchBendBinding(channel, paramId);
                }
                else
                {
                    var fourteen =
                        source.Equals("FourteenBitControlChange", System.StringComparison.OrdinalIgnoreCase)
                        || source.Equals("FourteenBit", System.StringComparison.OrdinalIgnoreCase)
                        || source.Equals("14bit", System.StringComparison.OrdinalIgnoreCase);
                    if (!source.Equals("ControlChange", System.StringComparison.OrdinalIgnoreCase)
                        && !fourteen
                        && !source.Equals("CC", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return
                            "[Error] vst3-cc-mapping-edit: source must be ControlChange, " +
                            "FourteenBitControlChange, or PitchBend.";
                    }

                    mapping.UpsertControlChangeBinding(channel, controller, paramId, fourteen);
                    if (fourteen && controllerLsb > 0 && mapping.Bindings != null)
                    {
                        var list = mapping.Bindings;
                        for (var i = 0; i < list.Length; i++)
                        {
                            var b = list[i];
                            if (b.source != VstMidiParameterMapping.SourceType.FourteenBitControlChange)
                                continue;
                            if (b.controller != controller)
                                continue;
                            if (b.parameterId != paramId)
                                continue;
                            b.controllerLsb = controllerLsb;
                            list[i] = b;
                            mapping.Bindings = list;
                            break;
                        }
                    }
                }

                EditorUtility.SetDirty(mapping);
                AssetDatabase.SaveAssets();
                return
                    $"[Success] vst3-cc-mapping-edit path={path} source={source} ch={channel} " +
                    $"cc={controller} paramId={paramId} bindings={mapping.Bindings?.Length ?? 0}";
            });
        }

        [AiTool("vst3-midi-learn", Title = "VST3 / MIDI Learn")]
        [Description(
            "Editor only — arm MIDI Learn on VstHostMidiParameterMapper for a parameter. " +
            "Next CC or Pitch Bend writes the Mapping asset. " +
            "Set bindNow=true to upsert immediately (controller / sourceHint) without waiting.")]
        public string MidiLearn
        (
            [Description("Loaded plugin instance id.")]
            int targetPluginId,
            [Description("Parameter id to learn.")]
            long parameterId,
            [Description("Mapping asset path. Required.")]
            string mappingAssetPath,
            [Description("Learn as 14-bit CC pair.")]
            bool learnAsFourteenBit = false,
            [Description(
                "CC number for bindNow ControlChange (ignored when bindNow=false or PitchBend).")]
            int controller = 1,
            [Description("When true, upsert binding now (no wait). When false, arm Learn for next CC/PB.")]
            bool bindNow = false,
            [Description("bindNow source: ControlChange | FourteenBitControlChange | PitchBend.")]
            string sourceHint = "ControlChange",
            [Description("MIDI channel for bind / learn context (-1 = all).")]
            int channel = -1,
            [Description("GameObject name. Empty = __VstHostMcpMidi.")]
            string? gameObjectName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var path = mappingAssetPath?.Trim().Replace('\\', '/') ?? string.Empty;
                var mapping = AssetDatabase.LoadAssetAtPath<VstMidiParameterMapping>(path);
                if (mapping == null)
                    return $"[Error] vst3-midi-learn: no mapping at '{path}'. Call vst3-cc-mapping-create.";

                if (targetPluginId < 1)
                    return "[Error] vst3-midi-learn: targetPluginId must be >= 1.";

                var go = FindOrCreate(gameObjectName);
                var mapper = go.GetComponent<VstHostMidiParameterMapper>();
                if (mapper == null)
                    mapper = go.AddComponent<VstHostMidiParameterMapper>();

                mapper.TargetPluginId = targetPluginId;
                mapper.Mapping = mapping;
                var paramId = unchecked((uint)parameterId);
                mapper.NotifyParameterTouched(paramId);

                var so = new SerializedObject(mapper);
                so.FindProperty("learnAsFourteenBit").boolValue = learnAsFourteenBit;
                so.FindProperty("persistLearnToMappingAsset").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (!mapper.IsRegistered)
                    mapper.Register();

                if (bindNow)
                {
                    if (sourceHint.Equals("PitchBend", System.StringComparison.OrdinalIgnoreCase)
                        || sourceHint.Equals("PB", System.StringComparison.OrdinalIgnoreCase))
                    {
                        mapping.UpsertPitchBendBinding(channel, paramId);
                        EditorUtility.SetDirty(mapping);
                        AssetDatabase.SaveAssets();
                        mapper.MidiLearn = false;
                        return
                            $"[Success] vst3-midi-learn bindNow PitchBend → paramId={paramId} path={path} " +
                            $"gameObject={go.name}";
                    }

                    var fourteen = learnAsFourteenBit
                        || sourceHint.Equals("FourteenBitControlChange", System.StringComparison.OrdinalIgnoreCase)
                        || sourceHint.Equals("14bit", System.StringComparison.OrdinalIgnoreCase);
                    mapping.UpsertControlChangeBinding(channel, controller, paramId, fourteen);
                    EditorUtility.SetDirty(mapping);
                    AssetDatabase.SaveAssets();
                    mapper.MidiLearn = false;
                    return
                        $"[Success] vst3-midi-learn bindNow CC={controller} fourteenBit={fourteen} " +
                        $"→ paramId={paramId} path={path} gameObject={go.name}";
                }

                mapper.MidiLearn = true;
                return
                    $"[Success] vst3-midi-learn armed gameObject={go.name} pluginId={targetPluginId} " +
                    $"paramId={paramId} fourteenBit={learnAsFourteenBit} path={path}. " +
                    "Send CC or Pitch Bend next; then vst3-activity-read / re-list mapping.";
            });
        }

        static string AssignMappingToMapper(
            string gameObjectName,
            VstMidiParameterMapping mapping,
            int targetPluginId)
        {
            var go = FindOrCreate(gameObjectName);
            var mapper = go.GetComponent<VstHostMidiParameterMapper>();
            if (mapper == null)
                mapper = go.AddComponent<VstHostMidiParameterMapper>();
            mapper.TargetPluginId = targetPluginId;
            mapper.Mapping = mapping;
            if (!mapper.IsRegistered)
                mapper.Register();
            return $" assignedTo={go.name} pluginId={targetPluginId} mapperRegistered={mapper.IsRegistered}";
        }

        static void EnsureAssetFolder(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            var current = parts[0];
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
