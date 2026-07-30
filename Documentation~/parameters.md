# Parameters, presets, and state

## Parameters

```csharp
var host = VstHostManager.Instance;
foreach (var p in host.GetParameters(pluginId))
{
    if (p.IsReadOnly) continue;
    host.TryGetParameterNormalized(pluginId, p.Id, out var v);
    host.SetParameterNormalized(pluginId, p.Id, 0.5);
}
```

`SetParameterNormalized` updates the edit controller and queues a realtime change for the next `Process` call.

MIDI CC / pitch bend → parameter mapping: see [midi-integration.md](midi-integration.md) (`VstMidiParameterMapping`, `VstHostMidiParameterMapper`).

## Programs / presets

Best-effort via `IUnitInfo` program lists, or a `kIsProgramChange` parameter:

```csharp
var names = host.GetPrograms(pluginId);
host.SetProgram(pluginId, 0);
```

Not all plugins expose program lists.

## State

Opaque blob (`VHS1` + component + controller bytes). Suitable for save/load in your project:

```csharp
byte[] state = host.GetState(pluginId);
host.SetState(pluginId, state);
```

### `VstPresetAsset`

Create via **Assets → Create → VST3 Host → Preset**.

| API | Purpose |
|-----|---------|
| `CaptureFrom(pluginId)` | Stores `GetState` + optional loaded-plugin metadata |
| `ApplyTo(pluginId)` | Restores via `SetState` |
| `HasState` | Whether a blob is present |

Inspector (Play Mode): Capture / Apply buttons. Menu **Window → VST3 Host → Preset Browser** lists project presets and host programs.

### Runtime A/B

Attach `VstPresetBrowser`: host program list, preset asset Apply, and A/B capture/restore/toggle.

## Simple UI

Attach `VstHostParameterPanel`, set `PluginId`, and use the on-screen sliders. Moving a slider notifies `VstHostMidiParameterMapper` on the same GameObject (MIDI Learn).

**Not supported:** embedding the plugin’s native editor (`IPlugView` / HWND).
