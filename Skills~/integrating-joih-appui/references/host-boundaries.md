# Project-Owned Host Boundaries

Use this reference when AppUI is installed but one or more required ports are
missing, uncertain, or coupled directly to a project backend. AppUI Core stays
neutral: the consumer owns all three implementations and the composition root
injects them explicitly.

```csharp
IUIOperationFactory operations = projectOperations;
IUIAssetProvider assets = projectAssets;
IAppUIExecutionContext execution = projectExecutionContext;

AppUIInitializationResult result = runtimeHost.Initialize(
    new AppUIRuntimeDependencies(operations, assets, execution));
```

Do not call `Initialize` until all three objects are non-null and owned by the
project. `AppUIRuntimeDependencies` provides no fallback.

## Responsibility and ownership table

| Port | Public contract | Project responsibility | AppUI responsibility |
|---|---|---|---|
| `IUIOperationFactory` | `Create<TResult>(AppUIOperationDescriptor)` returns an `IUIOperationSource<TResult>` with a non-null `Operation`. | Adapt the chosen callback, Task, Awaitable, coroutine, or other async model; own cancellation resources, terminal publication, thread safety, and subscriptions. | Consume only the neutral operation/source protocol and publish domain results through it. |
| `IUIAssetProvider` | `TryLoad<T>` and `Load<T>(assetId, CancellationToken)` return `UIAssetLoadResult<T>`, optionally with a `UIAssetLease`. | Map stable AssetIds to the chosen Addressables, AssetBundle, custom asset system, or explicit table; own backend handles and release callbacks. | Hold or transfer the Lease while a load/allocation is accepted, and return it on release or rejection. |
| `IAppUIExecutionContext` | `IsCurrent` and `Post(Action)` identify and enter the Unity commit context. | Adapt the project's existing main-thread dispatcher and keep it alive while AppUI operations can complete. | Route external completion commits through the injected context; never create a dispatcher or select a PlayerLoop implementation. |

These rows describe adapter shapes, not recommendations. First inspect the
consumer's existing async, asset, and main-thread services. Present compatible
adapters and ask for a choice when ownership cannot be inferred. Do not install
or silently select UniTask, a Task wrapper, Awaitable, Resources, Addressables,
an AssetBundle layer, or any other implementation to shorten the integration.

## Operation and subscription contract

The factory must return one producer source and one consumer operation for every
descriptor. The source publishes at most one terminal state: `Succeeded`,
`Cancelled`, `Failed`, or `Expired`. `RequestCancellation()` requests work to
stop; it does not manufacture a terminal state. A successful infrastructure
operation can still contain a failed AppUI domain result, so callers check both
the operation status and the result's `Success` value.

`IUIOperation<TResult>.Register(...)` returns `IDisposable`. The owner of a
long-lived callback stores and disposes that subscription during unbind or
destruction. Disposing a subscription removes that continuation; it is not a
replacement for `RequestCancellation()` and does not shut down the factory.
Operation adapters must also deliver the same terminal value to late
subscribers, as required by the public contract.

When a completion can arrive off-thread, AppUI checks `IsCurrent`; otherwise it
uses `Post` before committing Runtime state. The execution context must therefore
represent the consumer's real Unity-safe commit context. Keep lifecycle guards
at the owning component as well: subscription disposal and scene generation are
separate protections from thread marshalling.

## Asset and late-result cleanup

A successful `UIAssetLoadResult<T>` may carry a `UIAssetLease`. The Lease wraps
the provider's release action, and `UIAssetLease.Dispose()` is idempotent: the
callback runs at most once. The provider's underlying release path should also
be safe when cleanup paths converge.

Cancellation does not erase ownership. If an asset load produces a late result
after a page was cancelled, expired, rejected, or invalidated by scene release,
AppUI discards the asset and disposes the returned Lease. Invalid allocations and
failed Controller validation also return ownership. A pooling strategy may
retain a Lease only while it retains the corresponding live instance; eviction
or shutdown releases both.

Keep the asset provider alive until AppUI has released active allocations and
the project has evicted pools. Stopping the provider first can strand backend
handles even though AppUI later performs its required cleanup.

## Choose the learning or production path

- **Basic Integration** is the zero-third-party learning path. Its callback
  operation, explicit in-memory asset table, and Unity synchronization-context
  adapter are imported Sample code, not Runtime defaults. Replace any part that
  does not match the consumer's architecture.
- **Custom Host Integration** is the production-host reference for explicit
  scene bridges, world-input authority, pooling, observable Lease ownership,
  shutdown, and contract tests. Adapt it beside the project composition root;
  do not modify AppUI Core or Runtime to recognize the host.

For production adapters, run the public Host Integration Contract Test Kit. At
minimum cover `AppUIOperationFactoryContractFixture`,
`AppUIAssetProviderContractFixture`, `AppUIExecutionContextContractFixture`, and
`AppUIHostLifecycleContractFixture`; include
`AppUIInstanceStrategyContractFixture` when a custom pool/instance strategy is
present. Passing the three port contracts establishes the boundary; it does not
create or initialize the Runtime root.
