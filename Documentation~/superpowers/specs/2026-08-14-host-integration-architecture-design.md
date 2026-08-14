# Joi.H AppUI Host Integration Architecture

**Status:** Confirmed implementation baseline
**Target:** `v0.3.0-pre.1`
**Official Unity line:** Unity 6.0 / `6000.0`
**Previous immutable release:** `v0.2.0-pre.4`

## 1. Goal

Joi.H AppUI must integrate with an unknown host without changing AppUI Core,
Runtime, or Editor. The host may add only its own adapters, lifecycle bridge,
installer, optional strategies, Editor authoring resolver, and test driver.

AppUI does not officially depend on or implement GameFramework, YooAsset,
Addressables, UniTask, Task, Awaitable, Resources, or another host backend.

## 2. Non-goals

- Do not add a god interface such as `IAppUIFrameworkAdapter`.
- Do not add scene discovery, procedure discovery, or host lifecycle polling.
- Do not add event bus, localization, data table, network, save, or logging ports.
- Do not add a second Host Contract version number in 0.3.
- Do not open the third-party adapter ecosystem before a distribution license is
  selected.

## 3. Integration categories

### 3.1 Required Runtime Ports

Every runtime installation supplies exactly three capabilities:

- `IUIOperationFactory`: creates backend-neutral producer/consumer operations;
- `IUIAssetProvider`: maps host asset identifiers to Unity assets and leases;
- `IAppUIExecutionContext`: returns external completions to the Unity context.

They are passed through `AppUIRuntimeDependencies`. AppUI provides no fallback.

### 3.2 Optional Runtime Configuration

Optional strategies are supplied separately from required ports:

```text
AppUIRuntimeDependencies
├ OperationFactory
├ AssetProvider
└ ExecutionContext

AppUIRuntimeConfiguration
├ LoadStrategies
└ InstanceStrategies
```

`AppUIRuntimeHost.Initialize(dependencies, configuration)` must register and
validate the configuration before validating page definitions. The one-argument
overload uses an empty configuration.

Strategy identifiers are non-empty, ordinal strings. Duplicate identifiers and
unknown identifiers referenced by definitions are structured initialization
failures in every build type. Registration is never last-write-wins.

### 3.3 Lifecycle Bridges

The host owns scene/procedure/application lifecycle and calls AppUI entry points.
AppUI never discovers or controls host lifecycle.

```text
Host scene ready  -> BindScene
Host scene leave  -> UnbindScene / ReleaseScope
Host shutdown     -> AppUIRuntimeHost.Shutdown
```

The host may serialize transitions, but AppUI correctness must not depend on it.

### 3.4 Editor Authoring Port

`IUIEditorAssetIdResolver` is the Editor counterpart of
`IUIAssetProvider`. Runtime and Editor must use identical AssetId semantics.

Resolvers register under deterministic IDs. `UIBindingSettings` selects one ID.
Missing selection, missing resolver, and duplicate resolver ID are explicit
states with centralized diagnostics. Editor initialization order must not select
the active resolver implicitly.

The current implicit Resources resolver moves out of the default Editor path.
Basic Integration explicitly registers and selects its own sample resolver.

## 4. Authority and ownership

Authority follows ownership. One state has one final writer.

| Capability | Authority |
| --- | --- |
| Scene / procedure / application lifetime | Host |
| Business services | Host |
| Resource backend | Host |
| Async backend | Host |
| Unity execution context | Host |
| World input execution | Host |
| Page lifecycle | AppUI |
| Page stack / layer / scope | AppUI |
| Focus / UI input policy | AppUI |
| Binding contract | AppUI |

A concrete page has exactly one lifecycle authority. If AppUI owns the page,
the host calls `IUIService`; it must not also open, close, pool, or destroy that
page through a second UI manager.

### 4.1 Operation ownership

- The factory owns its operation implementation and cancellation resources.
- AppUI owns the producer source while executing an AppUI command.
- Terminal completion is written exactly once.
- Late registration receives the same terminal completion.
- Disposing one subscription prevents only that callback.
- `RequestCancellation` is a request, not a fabricated terminal completion.
- Composite AppUI operations propagate cancellation to their current child,
  stop starting new children, and publish one terminal result.

### 4.2 Asset ownership

- A successful provider result may carry one `UIAssetLease`.
- Before instance creation, AppUI owns that lease.
- A result that is rejected, cancelled, expired, or arrives late is disposed by
  AppUI exactly once.
- Provider lifetime extends beyond AppUI shutdown until all AppUI-owned leases
  have been returned.

### 4.3 Instance ownership

The 0.2 `LoadStrategyId + DestroyStrategyId` model is asymmetric. 0.3 replaces
it with `LoadStrategyId + InstanceStrategyId` and freezes these invariants before
freezing a public method shape:

1. Instance creation transfers the prefab lease atomically from AppUI to one
   instance lifetime owner.
2. A living pooled object always has valid resource ownership.
3. AppUI and an instance strategy never simultaneously own or release the same
   lease.
4. Returning an object to a pool does not imply asset release.
5. Pool eviction or pool shutdown destroys retained instances and returns their
   underlying leases.
6. Failed creation and unaccepted transfers remain AppUI-owned and are cleaned
   by AppUI.

The implementation may use an allocation/handle and a one-shot lease transfer.
The public API is accepted only after the contract tests demonstrate default,
failure, and pooling ownership paths.

## 5. SceneScope generation contract

Runtime epoch and per-page operation version do not identify a SceneScope
lifetime. AppUI adds an internal `(SceneScopeId, Generation)` stamp.

```text
Bind A / gen17
Open page / gen17
Unbind A
  -> synchronously invalidate gen17
  -> cancel active and pending opens stamped gen17
Bind A / gen18
old load / gen17 completes
  -> generation check rejects commit
  -> lease returned exactly once
```

The stamp travels with active opens, pending opens, instances, close work, and
commit checks. An old unbind may close only the retiring generation; it may not
close a newer generation that reused the same SceneScopeId or PageId.

`ReleaseScope` invalidates the current non-global scope generation before
building close work. `GlobalScope` remains governed by runtime epoch.

## 6. Composite cancellation contract

`BindScene`, `UnbindScene`, and `ReleaseScope` are composite operations.

- Outer cancellation requests cancellation on the current child operation.
- No later child starts after outer cancellation.
- Child completion after the outer operation is terminal is ignored.
- Child subscriptions and cancellation registrations are disposed exactly once.
- A child infrastructure failure fails the outer operation.
- A child domain failure is collected in the aggregate result and does not stop
  later rules unless the operation contract explicitly says so.

## 7. Runtime initialization contract

```text
Construct Required Ports
-> Construct Optional Configuration
-> Initialize(dependencies, configuration)
-> validate required ports
-> validate strategy IDs and duplicates
-> install built-in default strategies
-> install configured strategies
-> validate definitions
-> Runtime Ready
```

Failure leaves the runtime uninitialized and releases only AppUI-owned state.
Host-owned adapters and strategies are not disposed by AppUI. Reinitialization
after shutdown reapplies a complete dependency/configuration snapshot.

## 8. Editor resolver contract

The Editor resolver registry is keyed by stable ID and rejects duplicates.
`UIBindingSettings.SelectedAssetIdResolverId` selects the resolver used by all
Definition creation, Binding, validation, and command-line entry points.

Missing configuration reports:

```text
No Editor AssetId Resolver is configured. Configure one that uses the same
AssetId semantics as the runtime IUIAssetProvider.
```

Interactive diagnostics provide a Project Settings navigation action. CI emits
the same semantic error without opening UI.

## 9. Conformance Test Kit

The optional test assembly exposes inheritable contract fixtures and test-only
drivers. It does not add lifecycle/provider interfaces to Runtime.

Suites:

- Operation: success, failure, cancel, first-terminal-wins, early/late
  subscription, subscription disposal, concurrency;
- Asset: sync success, unsupported sync, not found, async failure, cancellation,
  completion after cancellation, one-shot lease, shutdown ordering;
- Execution: current context, foreign thread, exactly once, ordering, failure,
  shutdown boundary;
- Lifecycle: bind/unbind, pending load, scope invalidation, repeated and
  interleaved generations, runtime shutdown;
- Instance: create/release symmetry, failure cleanup, transfer ownership,
  pooled return, pool eviction.

Third-party tests implement test drivers in their own test assembly and inherit
the contract fixtures.

## 10. Samples

- **Basic Integration:** minimum three-port injection with no hidden defaults.
- **Custom Host Integration:** full composition root, optional configuration,
  scene bridge, world-input query, shutdown order, and conformance test driver.

Neither sample is registered by Runtime and neither names a third-party host.

## 11. Forbidden integration patterns

- Host UI manager and AppUI both owning the same page lifecycle;
- Runtime scanning host assemblies for adapters;
- Adapter registering an Editor resolver through unpredictable initialization
  order;
- Provider handle exposed to controllers;
- Pool retaining a GameObject after releasing its only asset ownership;
- host destroying providers before AppUI shutdown returns leases;
- AppUI depending on host scene serialization for correctness.

## 12. Versioning and licensing

The feature ships as `v0.3.0-pre.1`. Adapter packages declare an AppUI SemVer
range; 0.3 does not introduce a second contract version number.

Technical work, test kit, and samples may be completed before license selection.
Third-party adapter distribution, Community Verified status, and an external
adapter index are blocked until the repository has an explicit distribution
license.

## 13. Acceptance criteria

For an unknown host, integration adds only host-side adapters, lifecycle bridge,
installer, optional strategies, Editor resolver, and test driver. It changes no
AppUI Core, Runtime, or Editor source and uses no internal type. Contract tests
pass, and a clean external Consumer completes page loading, SceneScope exit,
late-result cleanup, instance release, runtime shutdown, Binding, EditMode,
PlayMode, Mono, IL2CPP, and Git-install validation.
