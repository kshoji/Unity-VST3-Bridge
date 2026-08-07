# Audio graph

`VstAudioGraph` renders a stereo **DAG** of VST3 nodes on one `AudioSource` via `OnAudioFilterRead`.

Disable any `VstHostAudioFilter` on the same GameObject when using a graph.

## Migration from `VstPluginChain` (removed in 1.3.0)

| Chain (1.2.x) | Graph (1.3.0+) |
|---------------|----------------|
| `SetSlots` + MixMode Parallel | `BuildParallelInstrumentsThenSerialEffects` |
| `SetSlots` + MixMode StrictSerial | `BuildStrictSerial` |
| `MixExternalInput` | `Build*(…, mixExternalInput: true)` / ExternalIn node |
| Channel route → **slot index** | Channel route → **instrument node id** |
| `SetBypass(pluginId, …)` | `SetBypass` / `SetBypassByPluginId` |

Send/Return and sidechain are first-class on Graph (`BuildSendReturn` / `BuildSidechain*`).

## Node kinds

| Kind | Behavior |
|------|----------|
| ExternalIn | Seeds from the Unity filter buffer (Chunity / upstream filters) |
| Instrument | `Process` with null input |
| Effect | Main in → out; optional Sidechain → `ProcessWithSidechain` (bypass = copy main × node gain) |
| Mix | N→1 sum (edge gains applied; no normalization) |
| Split | 1→N tap (send level = outgoing edge `gain`) |
| Gain | Multiply (bypass = unity copy) |
| Output | Exactly one; writes the final bus |

Ports: `Main` everywhere; `Sidechain` input on Effect only (Aux bus 0). Plugins without Aux ignore sidechain audio.

## Builders

```csharp
// Former Chain default MixMode
graph.BuildParallelInstrumentsThenSerialEffects(
    instrumentPluginIds, effectPluginIds, mixExternalInput: false);

graph.BuildStrictSerial(new[]
{
    VstGraphBuildSlot.Instrument(instId),
    VstGraphBuildSlot.Effect(fxId),
});

// Send / Return (Split + edge gains + Mix)
graph.BuildSendReturn(instrumentId, reverbOrFxId, sendGain: 0.3f, dryGain: 1f);
graph.SetEdgeGain(splitId, fxId, VstGraphPort.Main, 0.5f); // live send amount

// Sidechain (Effect Aux)
graph.BuildSidechain(padInstrumentId, kickInstrumentId, sidechainFxId);
// or same source → main + Aux (e.g. AGain SideChain check):
graph.BuildSidechainFromSingleSource(instrumentId, againSideChainId);
```

While playing, prefer `SetGraph` / `ClearGraph` / `SetBypass` / `SetNodeGain` / `SetEdgeGain` / `SetChannelRoutes`
(these arm immediately). Inspector or direct list edits arm on `LateUpdate` / `OnValidate`.

## Channel routes

MIDI channel → **instrument node id** via `ChannelRoutes` / `ResolveInstrumentPluginId`.
Use `VstHostChannelRouteSync` to align Adapter ↔ Graph routes.

## Constraints (V1)

- DAG only (cycles fail arm; previous snapshot kept)
- Stereo L/R, max **32** nodes, exactly **one** Output
- See [audio-graph-plan.md](audio-graph-plan.md) §14 for bypass / validation rules
- Sidechain verify plugin: SDK **AGain SideChain** ([verification.md](verification.md))
- Manual scenario matrix (Instrument / Effect / Parallel / Send / Sidechain / ExternalIn):
  [verification.md](verification.md) § Audio Graph (manual matrix)
- Automated: `Tests/Runtime/VstAudioGraphTests.cs`;
  `native~/windows-vst-host/Run-Phase5Verify.ps1`

## Editor visualization

**Window → VST3 Host → Audio Graph** opens a read-only view of the selected (or scene) `VstAudioGraph`:

- Nodes (`id` / `kind` / `pluginId` / `gain` / `bypass`)
- Edges (`from` → `to`, `toPort` Main|Sidechain, edge `gain`) with Send (Split) / Sidechain coloring
- ChannelRoutes and arm status (`HasArmedGraph`, last arm error; failed arm keeps the previous snapshot)
- Optional text diagram of connections

Wiring stays on Inspector lists / `Build*` / `SetGraph`. The component Inspector also has a short overview foldout and an **Open Audio Graph Window** button.
