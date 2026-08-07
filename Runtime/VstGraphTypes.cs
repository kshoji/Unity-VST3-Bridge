using System;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost
{
    public enum VstGraphNodeKind
    {
        ExternalIn = 0,
        Instrument = 1,
        Effect = 2,
        Mix = 3,
        Split = 4,
        Gain = 5,
        Output = 6,
    }

    public enum VstGraphPort
    {
        Main = 0,
        Sidechain = 1,
    }

    [Serializable]
    public struct VstGraphNode
    {
        public int id;
        public VstGraphNodeKind kind;
        [Tooltip("Instrument / Effect only.")]
        public int pluginId;
        [Range(0f, 2f)] public float gain;
        public bool bypass;

        public static VstGraphNode Create(int id, VstGraphNodeKind kind, int pluginId = 0, float gain = 1f)
        {
            return new VstGraphNode
            {
                id = id,
                kind = kind,
                pluginId = pluginId,
                gain = gain,
                bypass = false,
            };
        }
    }

    [Serializable]
    public struct VstGraphEdge
    {
        public int fromNodeId;
        public VstGraphPort fromPort;
        public int toNodeId;
        public VstGraphPort toPort;
        [Range(0f, 2f)] public float gain;

        public static VstGraphEdge Main(int fromNodeId, int toNodeId, float gain = 1f)
        {
            return new VstGraphEdge
            {
                fromNodeId = fromNodeId,
                fromPort = VstGraphPort.Main,
                toNodeId = toNodeId,
                toPort = VstGraphPort.Main,
                gain = gain,
            };
        }

        public static VstGraphEdge Sidechain(int fromNodeId, int toNodeId, float gain = 1f)
        {
            return new VstGraphEdge
            {
                fromNodeId = fromNodeId,
                fromPort = VstGraphPort.Main,
                toNodeId = toNodeId,
                toPort = VstGraphPort.Sidechain,
                gain = gain,
            };
        }
    }

    /// <summary>Slot descriptor for <see cref="VstAudioGraph.BuildStrictSerial"/>.</summary>
    [Serializable]
    public struct VstGraphBuildSlot
    {
        public int pluginId;
        public bool isInstrument;
        [Range(0f, 2f)] public float gain;

        public static VstGraphBuildSlot Instrument(int pluginId, float gain = 1f) =>
            new VstGraphBuildSlot { pluginId = pluginId, isInstrument = true, gain = gain };

        public static VstGraphBuildSlot Effect(int pluginId, float gain = 1f) =>
            new VstGraphBuildSlot { pluginId = pluginId, isInstrument = false, gain = gain };
    }
}
