#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Activity = jp.kshoji.unity.vst3nativehost.VstHostActivity;

namespace jp.kshoji.unity.vst3nativehost.mcp
{
    public partial class Tool_VstHost
    {
        [AiTool(
            "vst3-params-list",
            Title = "VST3 / Params List",
            ReadOnlyHint = true)]
        [Description(
            "List VST3 parameters for a loaded plugin (id, title, units, stepCount, flags, current normalized value). " +
            "preferAutomate=true lists CanAutomate continuous params first (skips ProgramChange / readOnly). " +
            "Requires loaded instance. Edit Mode OK when host is initialized.")]
        public string ParamsList
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Include hidden parameters.")]
            bool includeHidden = false,
            [Description("Prefer CanAutomate, non-programChange, non-readOnly params first.")]
            bool preferAutomate = false,
            [Description("Max parameters to print.")]
            int maxResults = 200
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-params-list", pluginId, out var error))
                    return error!;

                return
                    $"[Success] vst3-params-list preferAutomate={preferAutomate}\n" +
                    FormatParamsList(pluginId, includeHidden, maxResults, preferAutomate);
            });
        }

        [AiTool(
            "vst3-param-get",
            Title = "VST3 / Param Get",
            ReadOnlyHint = true)]
        [Description(
            "Get a normalized parameter value. Identify by paramId and/or title substring. " +
            "Edit Mode OK when host is initialized.")]
        public string ParamGet
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Parameter id from vst3-params-list. Optional if title is set.")]
            long? paramId = null,
            [Description("Title or shortTitle substring (case-insensitive). Optional if paramId is set.")]
            string? title = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-param-get", pluginId, out var error))
                    return error!;
                if (!TryResolveParam(pluginId, paramId, title, out var info, out var resolveErr))
                    return resolveErr!;

                if (!Host.TryGetParameterNormalized(pluginId, info.Id, out var value))
                    return $"[Error] vst3-param-get failed id={info.Id}";

                return
                    $"[Success] vst3-param-get pluginId={pluginId} paramId={info.Id} " +
                    $"title={info.Title} value={value:0.######} readOnly={info.IsReadOnly}";
            });
        }

        [AiTool("vst3-param-set", Title = "VST3 / Param Set")]
        [Description(
            "Set a normalized parameter (0–1). Identify by paramId and/or title. " +
            "Read-only params fail. Audible effect usually needs Play Mode + audio path.")]
        public string ParamSet
        (
            [Description("Loaded plugin instance id.")]
            int pluginId,
            [Description("Normalized value 0–1.")]
            double value,
            [Description("Parameter id. Optional if title is set.")]
            long? paramId = null,
            [Description("Title or shortTitle substring. Optional if paramId is set.")]
            string? title = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!TryRequireLoaded("vst3-param-set", pluginId, out var error))
                    return error!;
                if (!TryResolveParam(pluginId, paramId, title, out var info, out var resolveErr))
                    return resolveErr!;

                if (info.IsReadOnly)
                    return $"[Error] vst3-param-set: paramId={info.Id} title={info.Title} is read-only/hidden.";

                var clamped = value < 0 ? 0 : (value > 1 ? 1 : value);
                if (!Host.SetParameterNormalized(pluginId, info.Id, clamped))
                    return $"[Error] vst3-param-set failed paramId={info.Id}";

                Activity.PumpMainThread();
                Host.TryGetParameterNormalized(pluginId, info.Id, out var readBack);
                return
                    $"[Success] vst3-param-set pluginId={pluginId} paramId={info.Id} " +
                    $"title={info.Title} value={readBack:0.######} playMode={IsPlayMode}";
            });
        }
    }
}
