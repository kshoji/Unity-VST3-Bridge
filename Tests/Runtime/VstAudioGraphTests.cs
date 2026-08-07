using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace jp.kshoji.unity.vst3nativehost.Tests
{
    public sealed class VstAudioGraphTests
    {
        [Test]
        public void BuildParallel_CreatesExpectedTopology()
        {
            var go = new GameObject("VstAudioGraphTest");
            try
            {
                var graph = go.AddComponent<VstAudioGraph>();
                Assert.IsTrue(graph.BuildParallelInstrumentsThenSerialEffects(
                    new[] { 1, 2 }, new[] { 3 }, mixExternalInput: false));

                Assert.AreEqual(5, graph.Nodes.Count); // 2 inst + mix + fx + out
                Assert.IsTrue(graph.HasArmedGraph);

                var kinds = new List<VstGraphNodeKind>();
                foreach (var n in graph.Nodes)
                    kinds.Add(n.kind);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        VstGraphNodeKind.Instrument,
                        VstGraphNodeKind.Instrument,
                        VstGraphNodeKind.Mix,
                        VstGraphNodeKind.Effect,
                        VstGraphNodeKind.Output,
                    },
                    kinds);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildParallel_WithExternalIn_AddsSourceNode()
        {
            var go = new GameObject("VstAudioGraphTest");
            try
            {
                var graph = go.AddComponent<VstAudioGraph>();
                Assert.IsTrue(graph.BuildParallelInstrumentsThenSerialEffects(
                    new[] { 1 }, new int[0], mixExternalInput: true));
                Assert.AreEqual(4, graph.Nodes.Count); // ext + inst + mix + out
                Assert.AreEqual(VstGraphNodeKind.ExternalIn, graph.Nodes[0].kind);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildStrictSerial_InstrumentsAndEffects()
        {
            var go = new GameObject("VstAudioGraphTest");
            try
            {
                var graph = go.AddComponent<VstAudioGraph>();
                Assert.IsTrue(graph.BuildStrictSerial(new[]
                {
                    VstGraphBuildSlot.Instrument(1),
                    VstGraphBuildSlot.Effect(2),
                    VstGraphBuildSlot.Instrument(3),
                }));
                Assert.IsTrue(graph.HasArmedGraph);
                Assert.GreaterOrEqual(graph.Nodes.Count, 5);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetGraph_RejectsCycle_KeepsPreviousArm()
        {
            var go = new GameObject("VstAudioGraphTest");
            try
            {
                var graph = go.AddComponent<VstAudioGraph>();
                Assert.IsTrue(graph.BuildParallelInstrumentsThenSerialEffects(new[] { 1 }, new int[0]));
                Assert.IsTrue(graph.HasArmedGraph);

                var ok = graph.SetGraph(
                    new[]
                    {
                        VstGraphNode.Create(1, VstGraphNodeKind.Mix),
                        VstGraphNode.Create(2, VstGraphNodeKind.Mix),
                        VstGraphNode.Create(3, VstGraphNodeKind.Output),
                    },
                    new[]
                    {
                        VstGraphEdge.Main(1, 2),
                        VstGraphEdge.Main(2, 1),
                        VstGraphEdge.Main(2, 3),
                    });
                Assert.IsFalse(ok);
                Assert.IsTrue(graph.HasArmedGraph); // previous snapshot retained
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveInstrumentPluginId_UsesChannelRoute()
        {
            var go = new GameObject("VstAudioGraphTest");
            try
            {
                var graph = go.AddComponent<VstAudioGraph>();
                Assert.IsTrue(graph.BuildParallelInstrumentsThenSerialEffects(new[] { 10, 20 }, new int[0]));
                var instA = graph.Nodes[0].id;
                var instB = graph.Nodes[1].id;
                graph.SetChannelRoutes(new[]
                {
                    new VstAudioGraph.ChannelRoute { channel = 2, nodeId = instB },
                });

                Assert.AreEqual(20, graph.ResolveInstrumentPluginId(2));
                Assert.AreEqual(10, graph.ResolveInstrumentPluginId(0)); // fallback first instrument
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildSendReturn_CreatesSplitDryAndWet()
        {
            var go = new GameObject("VstAudioGraphTest");
            try
            {
                var graph = go.AddComponent<VstAudioGraph>();
                Assert.IsTrue(graph.BuildSendReturn(1, 2, sendGain: 0.25f, dryGain: 1f));
                Assert.IsTrue(graph.HasArmedGraph);

                var kinds = new List<VstGraphNodeKind>();
                foreach (var n in graph.Nodes)
                    kinds.Add(n.kind);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        VstGraphNodeKind.Instrument,
                        VstGraphNodeKind.Split,
                        VstGraphNodeKind.Effect,
                        VstGraphNodeKind.Mix,
                        VstGraphNodeKind.Output,
                    },
                    kinds);

                Assert.AreEqual(5, graph.Edges.Count);
                var sendEdge = default(VstGraphEdge);
                var foundSend = false;
                foreach (var e in graph.Edges)
                {
                    if (e.toPort != VstGraphPort.Main)
                        continue;
                    // Split → Effect carries sendGain
                    var to = FindKind(graph, e.toNodeId);
                    var from = FindKind(graph, e.fromNodeId);
                    if (from == VstGraphNodeKind.Split && to == VstGraphNodeKind.Effect)
                    {
                        sendEdge = e;
                        foundSend = true;
                    }
                }

                Assert.IsTrue(foundSend);
                Assert.AreEqual(0.25f, sendEdge.gain, 1e-5f);
                Assert.IsTrue(graph.SetEdgeGain(sendEdge.fromNodeId, sendEdge.toNodeId, VstGraphPort.Main, 0.5f));
                Assert.AreEqual(0.5f, FindEdgeGain(graph, sendEdge.fromNodeId, sendEdge.toNodeId, VstGraphPort.Main), 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildSidechain_WiresAuxEdge()
        {
            var go = new GameObject("VstAudioGraphTest");
            try
            {
                var graph = go.AddComponent<VstAudioGraph>();
                Assert.IsTrue(graph.BuildSidechain(10, 20, 30));
                Assert.IsTrue(graph.HasArmedGraph);

                var sideCount = 0;
                foreach (var e in graph.Edges)
                {
                    if (e.toPort == VstGraphPort.Sidechain)
                        sideCount++;
                }

                Assert.AreEqual(1, sideCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildSidechainFromSingleSource_Arms()
        {
            var go = new GameObject("VstAudioGraphTest");
            try
            {
                var graph = go.AddComponent<VstAudioGraph>();
                Assert.IsTrue(graph.BuildSidechainFromSingleSource(1, 2));
                Assert.IsTrue(graph.HasArmedGraph);
                Assert.AreEqual(4, graph.Nodes.Count);
                Assert.AreEqual(4, graph.Edges.Count);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static VstGraphNodeKind FindKind(VstAudioGraph graph, int nodeId)
        {
            foreach (var n in graph.Nodes)
            {
                if (n.id == nodeId)
                    return n.kind;
            }

            throw new System.InvalidOperationException("node not found: " + nodeId);
        }

        private static float FindEdgeGain(VstAudioGraph graph, int from, int to, VstGraphPort toPort)
        {
            foreach (var e in graph.Edges)
            {
                if (e.fromNodeId == from && e.toNodeId == to && e.toPort == toPort)
                    return e.gain;
            }

            throw new System.InvalidOperationException("edge not found");
        }
    }
}
