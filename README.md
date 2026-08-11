# Joi.H AppUI

`com.joih.appui` is a Unity 6 UPM framework for data-driven UGUI pages,
layers, lifecycle, binding, focus navigation, input passthrough, and lightweight
notices.

## Requirements

- Unity 6000.0 or newer
- UGUI 2.0
- UniTask 2.5.5
- TextMeshPro from the Unity UI stack

No third-party inspector package is required.

## Install

Add the package to `Packages/manifest.json` from a local path or Git URL. UniTask
must be resolvable as package `com.cysharp.unitask` version 2.5.5 or compatible.

```json
{
  "dependencies": {
    "com.joih.appui": "file:../JoiH-AppUI-Lab/package",
    "com.cysharp.unitask": "2.5.5"
  }
}
```

## Runtime setup

1. Create a UI root with `GlobalUIRoot`, `AppUIManager`, and
   `AppUIRuntimeHost`.
2. Add one `UILayerRoot` for every built-in layer.
3. Create `UIPageDefinition` assets and register them in a
   `UIPageDefinitionRegistry`.
4. Create an `AppUIRuntimeProfile` and assign the registry and optional layer
   and notice settings.
5. Use the built-in `ResourcesUIAssetProvider`, or inject a project adapter
   through `AppUIRuntimeHost.Initialize(IUIAssetProvider)`.

```csharp
IUIAssetProvider provider = new MyAddressablesUIProvider();
runtimeHost.Initialize(provider);

UIOpenResult result = await runtimeHost.Manager.Service.OpenAsync("settings");
```

The host application owns scene persistence, EventSystem creation, and shutdown.
Call `AppUIRuntimeHost.Shutdown()` before disposing a project-owned provider.

## Why UniTask is retained

UniTask supplies allocation-conscious `async`/`await` integration with Unity's
PlayerLoop. AppUI uses it for page loading, show/hide transitions, queued
operations, and asynchronous close/open flows without forcing `Task`-based
runtime scheduling.

## Documentation

- [Architecture](Documentation~/architecture.md)
- [Integration](Documentation~/integration.md)
- [Binding workflow](Documentation~/binding-workflow.md)
- [Validation](Documentation~/validation.md)

Import the **Basic Integration** sample from Package Manager for a code-only
provider adapter example.
