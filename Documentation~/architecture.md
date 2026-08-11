# Architecture

## Dependency direction

```text
Host application
  -> host adapters and composition root
    -> Joi.H AppUI Runtime
      -> Unity UGUI / TextMeshPro / UniTask
```

The package never references a concrete host application, scene, gameplay
action, or project resource service.

## Runtime modules

- `AppUIRuntimeHost`: scene composition boundary and provider injection.
- `AppUIManager`: public page/service facade and runtime orchestration.
- `Definition`: page/group data assets and indexed registries.
- `Layer` and `Stack`: Canvas domains, ordering, visibility, pause, and input
  depth.
- `Operation`: versioned open/close coordination and stale continuation guards.
- `SceneBinding`: explicit scene-scope enter, exit, and batch release.
- `Selection`: semantic focus graph, regions, groups, nodes, commit authority,
  scrolling, and trace diagnostics.
- `Input`: raycast-based blocking using generic input channels and authored
  passthrough zones.
- `Notice`: pooled Toast, Tooltip, FloatingText, and numeric notice views.
- `Binding`: editor scanning, generated partial code, prefab binding, ownership
  validation, and build-time read-only checks.

## Lifecycle

```text
Host.Initialize(provider)
  -> Manager.Initialize(...)
    -> validate definitions and layers
    -> configure notice pools

OpenAsync(pageId)
  -> resolve definition and strategy
  -> provider.LoadAsync(prefabId)
  -> instantiate and validate one PanelBaseController
  -> OnCreate -> OnInit -> data/refresh -> ShowAsync
  -> publish stack, input, and focus state

CloseAsync(pageId)
  -> CanClose -> remove interaction authority -> HideAsync
  -> OnDispose -> destroy -> UIAssetLease.Dispose

Host.Shutdown()
  -> release provider-backed notice assets
  -> clear provider reference
```

Every asynchronous page operation carries a version. Continuations that resume
after a newer operation or scope change are rejected before committing state.

## Asset ownership

`IUIAssetProvider` returns `UIAssetLoadResult<T>`. The optional `UIAssetLease`
encapsulates provider-specific release work and is disposed at most once. AppUI
therefore does not need to know whether the asset came from Resources,
Addressables, an AssetBundle, or a remote cache.

## Input ownership

Applications map concrete input actions to `AppUIInputChannel`. AppUI policies
only decide whether the chosen channel is blocked at a screen position.
Interactive `Selectable` hits always block passthrough. Non-interactive regions
use `BlockAll`, `PassAll`, `BlockInteractiveOnly`, or `PassChannelMask`.

## Assembly boundaries

- `Joi.H.AppUI.Runtime`: player-safe runtime API and implementation.
- `Joi.H.AppUI.Editor`: generation and validation tools; never enters builds.
- `Joi.H.AppUI.Tests.Editor`: EditMode contract tests.
- `Joi.H.AppUI.Tests.Runtime`: PlayMode contract tests.
