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

## Simple UI

Attach `VstHostParameterPanel`, set `PluginId`, and use the on-screen sliders.

**Not supported (Phase 6):** embedding the plugin’s native editor (`IPlugView` / HWND).
