# Runtime Root, Scene Scope, and Shutdown

Use this reference after the project has chosen and implemented all three host
ports. The goal is an explicitly initialized Runtime with no business page yet.
The consumer owns root creation, persistence, scene timing, and destruction.

## Author the Runtime root

Use an existing project composition root when possible. A minimal authored shape
is:

```text
AppUIRoot (project-owned lifetime)
├── GlobalUIRoot
├── AppUIManager
├── AppUIRuntimeHost
└── one or more layer objects
    └── UILayerRoot
```

Check each part deliberately:

- **EventSystem:** the scene or persistent UI environment has the project's one
  intended EventSystem and input module. AppUI consumes UGUI interaction; it
  does not create or own the EventSystem.
- **AppUIManager:** this is the Runtime facade behind `IUIService`. Do not let a
  second UI manager also open, close, pool, or destroy AppUI-owned pages.
- **AppUIRuntimeHost:** assign the existing `AppUIManager`, root/profile, registry,
  and layer references. Its reference resolution can find already-authored
  components; it does not create scene roots or choose a lifecycle.
- **UILayerRoot:** configure a unique `UILayerId`, its `UICanvasDomain`, and the
  `RectTransform` `ContentRoot`. Every future Definition must name a Layer and
  CanvasDomain that exist and match.
- **UIPageDefinitionRegistry:** create and assign a registry. It may contain no
  business page at this stage, but the Runtime cannot initialize without a
  registry object.
- **AppUIRuntimeProfile:** assign the registry and any project-selected layer or
  notice settings, then assign the profile to the host. Optional strategies stay
  in an immutable `AppUIRuntimeConfiguration`; do not invent one when the project
  has not chosen it.

Decide whether `AppUIRoot` is global/persistent or scene-owned in the host
architecture. AppUI does not infer that choice from hierarchy, `DontDestroyOnLoad`,
or Unity scene callbacks.

## Initialize explicitly and handle the result

Initialize from the project composition root after its adapters and authored
references are ready:

```csharp
IUIOperationFactory operations = projectOperations;
IUIAssetProvider assets = projectAssets;
IAppUIExecutionContext execution = projectExecutionContext;

AppUIInitializationResult result = runtimeHost.Initialize(
    new AppUIRuntimeDependencies(operations, assets, execution));

if (!result.Success)
{
    Debug.LogError("AppUI initialization failed: " + result.Status);
    if (result.Exception != null)
    {
        Debug.LogException(result.Exception);
    }
    return;
}
```

Do not expose `runtimeHost.Manager.Service` to business code until the result is
successful. `AlreadyInitialized` is a successful result for the same dependency
and configuration references; different dependencies/configuration produce a
structured failure. Missing ports, manager, registry, invalid strategy IDs, and
invalid optional configuration are also represented by
`AppUIInitializationResult` status instead of a fallback.

## Bind, release, and rebind scene scope

Scene timing belongs to the host. When a scene/procedure is ready, call
`SceneUIBinding.Bind(IUIService)` or `IUIService.BindScene(SceneUIBindingData)`.
When it leaves, call the matching `UnbindScene` or explicitly call
`ReleaseScope(UIPageScope.SceneScope, sceneScopeId)`. Save and dispose each
operation `Register` subscription owned by the scene bridge.

Use a stable, explicit `SceneScopeId` as the ownership label; it is not a Unity
Scene reference. `UnbindScene` and non-global `ReleaseScope` invalidate the
current internal generation before old asynchronous work can commit. A later
`BindScene` may rebind the same ID with a new generation, while a late result
from the old generation remains ineligible to mutate the new scene and still
returns its Lease.

Prefer this host sequence for a same-ID rebind:

1. Stop requests owned by the leaving scene.
2. Start `UnbindScene` (or the appropriate `ReleaseScope`) and observe its
   operation result.
3. Dispose leaving-scene subscriptions and references.
4. Bind the new scene data when the new host scene is ready.

Generation checks protect races; they do not authorize skipping scene cleanup.
`ReleaseScope` does not release `GlobalScope` pages. Global ownership must be
closed or released by its explicit host/runtime policy.

## Shutdown in ownership order

Use this order for application exit, UI-subsystem replacement, or destruction of
the project-owned root:

```text
stop new UI requests
→ ReleaseScope or UnbindScene
→ AppUIRuntimeHost.Shutdown
→ evict project pools and return Leases
→ stop asset provider
→ destroy project-owned UI root
```

Observe the scope/scene release operation while the Runtime is still available
when the host flow permits it. `AppUIRuntimeHost.Shutdown()` synchronously stops
the manager, invalidates the Runtime epoch, cancels outstanding intents, releases
active allocations, clears services, and drops injected references. After that,
project pools can evict retained instances and Leases, and only then may the
asset provider stop. Finally destroy the consumer-owned GameObjects.

`Shutdown()` is safe to call when already stopped, and a later explicit
`Initialize` may provide a new dependency set. This does not make AppUI a scene
lifecycle observer; the host must still call every transition.

## Runtime-root acceptance

Before moving to a first page, record evidence that:

- initialization returned success and `runtimeHost.IsInitialized` is true;
- the intended EventSystem, registry/profile, and Layer/CanvasDomain mappings
  are the authored project objects;
- Bind, Unbind/Release, and same-ID rebind complete through neutral operations;
- no old-generation completion commits after rebind, while late Leases return;
- Shutdown returns active ownership before pools/provider/root are destroyed;
- initialization failure status and exception evidence are surfaced rather than
  hidden by an Awake-order retry or Resources fallback.

This is an initialized Runtime boundary, not a completed page integration.
