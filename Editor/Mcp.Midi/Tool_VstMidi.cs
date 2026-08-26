#nullable enable
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.mcp.midi
{
    /// <summary>Editor-only MCP helpers for CC mapping tools. Runtime wiring is in Mcp.Midi.Runtime.</summary>
    public partial class Tool_VstMidi
    {
        internal const string DefaultObjectName = "__VstHostMcpMidi";

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
    }
}
