# Neutral Operation Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Joi.H AppUI 重构为可直接安装编译、无 UniTask 与默认 Resources 实现、由接入项目显式注入异步和资源能力的中立 Operation UI 框架。

**Architecture:** 新增 `Joi.H.AppUI.Core` 契约程序集，只定义 Operation、资源 Provider 与执行上下文协议；`Joi.H.AppUI.Runtime` 使用宿主注入的 OperationFactory 驱动显式 UI 状态机。所有原 UniTask await 链改为 continuation 阶段函数，Runtime 不实现 Task/UniTask/Awaitable/Coroutine 后端。

**Tech Stack:** Unity 6.0 (`6000.0.25f1`)、C#、UGUI 2.0、NUnit/Unity Test Framework 1.4.5、UPM、PowerShell。

## Global Constraints

- UPM Package ID 保持 `com.joih.appui`，根命名空间保持 `Joi.H.AppUI`。
- 目标版本为 `0.2.0-pre.1`。
- Runtime 不依赖 UniTask、Task、ValueTask、Unity Awaitable、IEnumerator 异步后端、Addressables 或项目资源系统。
- 删除 `ResourcesUIAssetProvider`、`InitializeOnAwake`、`UseResourcesProviderWhenMissing` 和所有自动 Provider fallback。
- Core 不提供 OperationFactory、AssetProvider 或 ExecutionContext 默认实现。
- 保留 `System.Threading.CancellationToken` 作为中立取消信号。
- AppUI 继续拥有 Definition、Controller、Layer、Scope、Binding、Focus、Input 和页面生命周期规则。
- 所有生产代码遵守测试先行：先写失败测试并确认失败原因，再写最小实现。
- 不修改 `D:\UGit\Annals-Base\Annals`；所有实现只发生在独立包和独立消费测试项目。
- 未经用户确认不推送 GitHub；每个任务可以创建本地提交。

---

## File Structure

### New Core contracts

- `Runtime/Core/Joi.H.AppUI.Core.asmdef`：Core 程序集边界，不引用任何第三方异步包。
- `Runtime/Core/Operation/AppUIOperationStatus.cs`：Operation 状态枚举。
- `Runtime/Core/Operation/AppUIOperationCompletion.cs`：不可变终态值。
- `Runtime/Core/Operation/AppUIOperationDescriptor.cs`：工厂创建上下文与外部取消 token。
- `Runtime/Core/Operation/UIUnit.cs`：无业务返回值 Operation 的显式成功值。
- `Runtime/Core/Operation/IUIOperation.cs`：消费端中立句柄。
- `Runtime/Core/Operation/IUIOperationSource.cs`：Runtime 生产端完成接口。
- `Runtime/Core/Operation/IUIOperationFactory.cs`：宿主注入工厂协议。
- `Runtime/Core/Execution/IAppUIExecutionContext.cs`：Unity 主线程回送协议。
- `Runtime/Core/AssetLoading/IUIAssetProvider.cs`：资源 Provider 协议。
- `Runtime/Core/AssetLoading/UIAssetLease.cs`：资源租约。
- `Runtime/Core/AssetLoading/UIAssetLoadResult.cs`：Provider-neutral 资源结果。

### New Runtime composition and transition types

- `Runtime/Bootstrap/AppUIRuntimeDependencies.cs`：三项强制注入依赖。
- `Runtime/Bootstrap/AppUIInitializationResult.cs`：结构化初始化状态与错误。
- `Runtime/Operation/UIOperationObserver.cs`：仅供 Runtime 使用的订阅、主线程回送和一次完成辅助逻辑。
- `Runtime/Controller/UITransition.cs`：Immediate 或 WaitFor 中立 Operation 的过渡描述。
- `Runtime/Controller/UITransitionResult.cs`：Controller 动画完成值。

### New test and sample implementations

- `Tests/Shared/ManualUIOperationFactory.cs`：确定性测试实现，不进入 Runtime。
- `Tests/Shared/ImmediateAppUIExecutionContext.cs`：测试主线程执行上下文。
- `Tests/Shared/Joi.H.AppUI.Tests.Shared.asmdef`：Editor/Runtime 测试共用的测试程序集。
- `Tests/Editor/AppUIOperationContractTests.cs`：Operation 契约测试。
- `Tests/Editor/AppUIRuntimeHostTests.cs`：显式初始化测试。
- `Tests/Editor/AppUIAsyncBoundaryTests.cs`：源码/asmdef/package 静态边界测试。
- `Samples~/Basic Integration/Callback/CallbackUIOperationFactory.cs`：用户主动导入 Sample 后才出现的纯回调实现。
- `Samples~/Basic Integration/Callback/UnityMainThreadExecutionContext.cs`：Sample 主线程上下文。
- `Samples~/Basic Integration/Callback/InMemoryUIAssetProvider.cs`：Sample 显式资源实现，不使用 Resources。

### Main modified runtime files

- `Runtime/AppUIManager.cs`
- `Runtime/IUIService.cs`
- `Runtime/Operation/UIOperationTypes.cs`
- `Runtime/Operation/UIOperationCoordinator.cs`
- `Runtime/Bootstrap/AppUIRuntimeHost.cs`
- `Runtime/Controller/UIBaseController.cs`
- `Runtime/Controller/PanelBaseController.cs`
- `Runtime/AssetLoading/IUIAssetProvider.cs`（移动到 Core 后删除旧位置）
- `Runtime/AssetLoading/UIAssetLease.cs`（移动到 Core 后删除旧位置）
- `Runtime/AssetLoading/UIAssetLoadResult.cs`（移动到 Core 后删除旧位置）
- `Runtime/AssetLoading/ResourcesUIAssetProvider.cs`（删除）
- `Runtime/Strategy/IUILoadStrategy.cs`
- `Runtime/Strategy/DefaultUILoadStrategy.cs`
- `Runtime/SceneBinding/SceneUIBinding.cs`
- `Runtime/SceneBinding/UISceneScopeCoordinator.cs`
- `Runtime/Flow/UIFlowTypes.cs`
- `Runtime/Flow/AppUIFlowCoordinator.cs`
- `Runtime/Selection/AppUIFocusScrolling.cs`
- `Runtime/Selection/AppUIFocusScope.cs`
- `Runtime/Input/UIBackgroundClickHandler.cs`
- `Runtime/Joi.H.AppUI.Runtime.asmdef`

### Packaging, samples, tests, docs

- `package.json`
- `Samples~/Basic Integration/SampleAppUIInstaller.cs`
- `Samples~/Basic Integration/Joi.H.AppUI.Samples.Basic.asmdef`
- `Tests/Editor/Joi.H.AppUI.Tests.Editor.asmdef`
- `Tests/Runtime/Joi.H.AppUI.Tests.Runtime.asmdef`
- `Tests/Runtime/AppUIRuntimeBoundaryTests.cs`
- `D:/UGit/JoiH-AppUI-Lab/UnityTestProject/Packages/manifest.json`
- `README.md`
- `Documentation~/index.md`
- `Documentation~/getting-started.md`
- `Documentation~/core-concepts.md`
- `Documentation~/architecture.md`
- `Documentation~/page-system.md`
- `Documentation~/lifecycle.md`
- `Documentation~/faq.md`
- `Documentation~/integration.md`
- `Documentation~/validation.md`
- `Samples~/Basic Integration/README.md`
- `CHANGELOG.md`

---

### Task 1: Introduce Core Operation contracts with no implementation

**Files:**
- Create: `Runtime/Core/Joi.H.AppUI.Core.asmdef`
- Create: `Runtime/Core/Operation/AppUIOperationStatus.cs`
- Create: `Runtime/Core/Operation/AppUIOperationCompletion.cs`
- Create: `Runtime/Core/Operation/AppUIOperationDescriptor.cs`
- Create: `Runtime/Core/Operation/UIUnit.cs`
- Create: `Runtime/Core/Operation/IUIOperation.cs`
- Create: `Runtime/Core/Operation/IUIOperationSource.cs`
- Create: `Runtime/Core/Operation/IUIOperationFactory.cs`
- Create: `Runtime/Core/Execution/IAppUIExecutionContext.cs`
- Create: `Tests/Shared/ManualUIOperationFactory.cs`
- Create: `Tests/Shared/ImmediateAppUIExecutionContext.cs`
- Create: `Tests/Shared/Joi.H.AppUI.Tests.Shared.asmdef`
- Create: `Tests/Editor/AppUIOperationContractTests.cs`
- Modify: `Runtime/Joi.H.AppUI.Runtime.asmdef`
- Modify: `Tests/Editor/Joi.H.AppUI.Tests.Editor.asmdef`
- Modify: `Tests/Runtime/Joi.H.AppUI.Tests.Runtime.asmdef`

**Interfaces:**
- Produces: `IUIOperation<TResult>`, `IUIOperationSource<TResult>`, `IUIOperationFactory.Create<TResult>(AppUIOperationDescriptor)`, `IAppUIExecutionContext.Post(Action)`.
- Consumes: `System.Threading.CancellationToken`, `System.Action`, `System.IDisposable` only.

- [ ] **Step 1: Write failing Operation contract tests**

Add tests that express the public protocol before the types exist:

```csharp
[Test]
public void Source_FirstTerminalWriteWins_AndLateSubscriberSeesSameCompletion()
{
    ManualUIOperationFactory factory = new ManualUIOperationFactory();
    IUIOperationSource<int> source = factory.Create<int>(
        AppUIOperationDescriptor.Create("contract"));
    AppUIOperationCompletion<int> first = default;
    AppUIOperationCompletion<int> late = default;

    source.Operation.Register(value => first = value);

    Assert.That(source.TrySetSucceeded(7), Is.True);
    Assert.That(source.TrySetFailed(new InvalidOperationException()), Is.False);
    source.Operation.Register(value => late = value);

    Assert.That(first.Status, Is.EqualTo(AppUIOperationStatus.Succeeded));
    Assert.That(first.Result, Is.EqualTo(7));
    Assert.That(late.Status, Is.EqualTo(first.Status));
    Assert.That(late.Result, Is.EqualTo(first.Result));
}

[Test]
public void Subscription_Dispose_PreventsOnlyThatCallback()
{
    ManualUIOperationFactory factory = new ManualUIOperationFactory();
    IUIOperationSource<int> source = factory.Create<int>(
        AppUIOperationDescriptor.Create("subscription"));
    int disposedCount = 0;
    int activeCount = 0;
    IDisposable disposed = source.Operation.Register(_ => disposedCount++);
    source.Operation.Register(_ => activeCount++);

    disposed.Dispose();
    source.TrySetSucceeded(1);

    Assert.That(disposedCount, Is.Zero);
    Assert.That(activeCount, Is.EqualTo(1));
}

[Test]
public void RequestCancellation_SignalsOperationToken_BeforeTerminalCompletion()
{
    ManualUIOperationFactory factory = new ManualUIOperationFactory();
    IUIOperationSource<int> source = factory.Create<int>(
        AppUIOperationDescriptor.Create("cancel"));

    Assert.That(source.Operation.RequestCancellation(), Is.True);
    Assert.That(source.Operation.CancellationToken.IsCancellationRequested, Is.True);
    Assert.That(source.Operation.Status, Is.EqualTo(AppUIOperationStatus.Cancelling));
    Assert.That(source.TrySetCancelled(), Is.True);
}
```

- [ ] **Step 2: Run EditMode tests and verify RED**

Run:

```powershell
& 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\UGit\JoiH-AppUI-Lab\UnityTestProject' -runTests -testPlatform EditMode -testFilter 'Joi.H.AppUI.Tests.AppUIOperationContractTests' -testResults 'D:\UGit\JoiH-AppUI-Lab\extraction\neutral-operation-task1-red.xml' -logFile 'D:\UGit\JoiH-AppUI-Lab\extraction\neutral-operation-task1-red.log'
```

Expected: compilation fails because `IUIOperationSource<>`, `AppUIOperationDescriptor` and related Core types do not yet exist.

- [ ] **Step 3: Add the minimal Core interfaces and values**

Use this exact contract surface:

```csharp
public interface IUIOperation<TResult>
{
    AppUIOperationStatus Status { get; }
    bool IsTerminal { get; }
    CancellationToken CancellationToken { get; }
    bool RequestCancellation();
    IDisposable Register(Action<AppUIOperationCompletion<TResult>> continuation);
    bool TryGetCompletion(out AppUIOperationCompletion<TResult> completion);
}

public interface IUIOperationSource<TResult>
{
    IUIOperation<TResult> Operation { get; }
    bool TrySetRunning();
    bool TrySetSucceeded(TResult result);
    bool TrySetCancelled();
    bool TrySetFailed(Exception exception);
    bool TrySetExpired();
}

public interface IUIOperationFactory
{
    IUIOperationSource<TResult> Create<TResult>(AppUIOperationDescriptor descriptor);
}

public interface IAppUIExecutionContext
{
    bool IsCurrent { get; }
    void Post(Action continuation);
}
```

`AppUIOperationCompletion<TResult>` has four static factories: `Succeeded`, `Cancelled`, `Failed`, `Expired`. `Failed` throws `ArgumentNullException` for a null exception. `AppUIOperationDescriptor.Create(name, cancellationToken)` normalizes null names to `string.Empty`. `UIUnit.Value` is the only value used for resultless success.

Core asmdef content:

```json
{
  "name": "Joi.H.AppUI.Core",
  "rootNamespace": "Joi.H.AppUI",
  "references": [],
  "autoReferenced": true,
  "noEngineReferences": false
}
```

Runtime and `Joi.H.AppUI.Tests.Shared` reference `Joi.H.AppUI.Core`; both existing test asmdefs reference the Shared test assembly. Do not add any async package reference.

- [ ] **Step 4: Implement the test-only manual factory**

`ManualUIOperationFactory` stores callbacks in insertion order, snapshots them before notification, supports late registration, links the descriptor token with its own `CancellationTokenSource`, and rejects all terminal writes after the first. It lives only under `Tests/Shared`.

- [ ] **Step 5: Run focused and full EditMode tests**

Expected: focused tests pass; existing EditMode suite remains green.

- [ ] **Step 6: Commit Task 1**

```powershell
git add Runtime/Core Runtime/Joi.H.AppUI.Runtime.asmdef Tests/Shared Tests/Editor/AppUIOperationContractTests.cs Tests/Editor/Joi.H.AppUI.Tests.Editor.asmdef Tests/Runtime/Joi.H.AppUI.Tests.Runtime.asmdef
git commit -m "Add neutral AppUI operation contracts"
```

---

### Task 2: Require explicit runtime dependency injection

**Files:**
- Create: `Runtime/Bootstrap/AppUIRuntimeDependencies.cs`
- Create: `Runtime/Bootstrap/AppUIInitializationResult.cs`
- Create: `Tests/Editor/AppUIRuntimeHostTests.cs`
- Modify: `Runtime/Bootstrap/AppUIRuntimeHost.cs`
- Modify: `Runtime/AppUIManager.cs`
- Modify: `Samples~/Basic Integration/SampleAppUIInstaller.cs`

**Interfaces:**
- Consumes: Task 1 `IUIOperationFactory`, `IAppUIExecutionContext`; existing `IUIAssetProvider` until Task 3 moves it.
- Produces: `AppUIRuntimeHost.Initialize(AppUIRuntimeDependencies)`, `AppUIInitializationResult`, manager dependency storage and epoch.

- [ ] **Step 1: Write failing initialization tests**

Cover each missing dependency without relying on log text:

```csharp
[Test]
public void Initialize_MissingOperationFactory_DoesNotEnterInitializedState()
{
    HostFixture fixture = HostFixture.CreateValid();
    AppUIRuntimeDependencies dependencies = new AppUIRuntimeDependencies(
        null,
        fixture.AssetProvider,
        fixture.ExecutionContext);

    AppUIInitializationResult result = fixture.Host.Initialize(dependencies);

    Assert.That(result.Status,
        Is.EqualTo(AppUIInitializationStatus.MissingOperationFactory));
    Assert.That(fixture.Host.IsInitialized, Is.False);
}

[Test]
public void Initialize_DifferentDependenciesWhileRunning_IsRejected()
{
    HostFixture fixture = HostFixture.CreateValid();
    AppUIRuntimeDependencies first = fixture.CreateDependencies();
    AppUIRuntimeDependencies second = fixture.CreateDependencies();

    Assert.That(fixture.Host.Initialize(first).Success, Is.True);
    AppUIInitializationResult repeated = fixture.Host.Initialize(second);

    Assert.That(repeated.Status,
        Is.EqualTo(AppUIInitializationStatus.AlreadyInitializedWithDifferentDependencies));
}
```

Also test missing `AssetProvider`, missing `ExecutionContext`, missing manager, missing registry, same-instance idempotence, Shutdown followed by reinitialize.

- [ ] **Step 2: Run focused EditMode tests and verify RED**

Expected: tests fail because the dependencies/result types and overload do not exist.

- [ ] **Step 3: Implement immutable dependencies and structured result**

Use constructor injection only:

```csharp
public sealed class AppUIRuntimeDependencies
{
    public AppUIRuntimeDependencies(
        IUIOperationFactory operationFactory,
        IUIAssetProvider assetProvider,
        IAppUIExecutionContext executionContext)
    {
        OperationFactory = operationFactory;
        AssetProvider = assetProvider;
        ExecutionContext = executionContext;
    }

    public IUIOperationFactory OperationFactory { get; }
    public IUIAssetProvider AssetProvider { get; }
    public IAppUIExecutionContext ExecutionContext { get; }
}
```

`AppUIInitializationStatus` contains `Success`, `AlreadyInitialized`, `MissingDependencies`, `MissingOperationFactory`, `MissingAssetProvider`, `MissingExecutionContext`, `MissingManager`, `MissingRegistry`, `InvalidLayerConfiguration`, `AlreadyInitializedWithDifferentDependencies`, `DependencyContractFailed`.

- [ ] **Step 4: Replace Host automatic initialization**

Remove serialized `initializeOnAwake` and `useResourcesProviderWhenMissing`. `Awake` calls only `ResolveSceneReferences()`. Replace `Initialize(IUIAssetProvider)` and `SetAssetProvider` with:

```csharp
public AppUIInitializationResult Initialize(AppUIRuntimeDependencies dependencies)
{
    AppUIInitializationResult validation = ValidateInitialization(dependencies);
    if (!validation.Success)
    {
        return validation;
    }

    if (initialized)
    {
        return ReferenceEquals(this.dependencies, dependencies)
            ? AppUIInitializationResult.AlreadyInitialized()
            : AppUIInitializationResult.Failure(
                AppUIInitializationStatus.AlreadyInitializedWithDifferentDependencies);
    }

    uiManager.Initialize(resolvedRegistry, dependencies, layerRoots,
        resolvedLayerSettings, resolvedNoticeSettings);
    this.dependencies = dependencies;
    initialized = true;
    return AppUIInitializationResult.Ok();
}
```

Validation occurs before mutating manager or host state. `Shutdown` clears the exact dependencies object and increments manager epoch.

- [ ] **Step 5: Add manager dependency guards**

`AppUIManager.Initialize` receives `AppUIRuntimeDependencies`. Public service access before successful initialization throws `InvalidOperationException` with the stable prefix `<Joi.H.AppUI> Runtime is not initialized.`. Do not add a fallback factory or provider.

- [ ] **Step 6: Keep the Sample compiling with an explicit temporary test composition**

Change `SampleAppUIInstaller` so it no longer calls the removed provider-only overload. Until Task 6 adds the final Sample implementations, guard the initialization call behind serialized `MonoBehaviour` fields implementing the three interfaces and report a clear setup error when any are absent.

- [ ] **Step 7: Run focused tests and full EditMode suite**

Expected: all initialization tests pass and the previous suite stays green.

- [ ] **Step 8: Commit Task 2**

```powershell
git add Runtime/Bootstrap Runtime/AppUIManager.cs Samples~/Basic\ Integration/SampleAppUIInstaller.cs Tests/Editor/AppUIRuntimeHostTests.cs
git commit -m "Require explicit AppUI runtime dependencies"
```

---

### Task 3: Convert page operations, asset loading, and controller transitions

**Files:**
- Move: `Runtime/AssetLoading/IUIAssetProvider.cs` → `Runtime/Core/AssetLoading/IUIAssetProvider.cs`
- Move: `Runtime/AssetLoading/UIAssetLease.cs` → `Runtime/Core/AssetLoading/UIAssetLease.cs`
- Move: `Runtime/AssetLoading/UIAssetLoadResult.cs` → `Runtime/Core/AssetLoading/UIAssetLoadResult.cs`
- Create: `Runtime/Operation/UIOperationObserver.cs`
- Create: `Runtime/Controller/UITransition.cs`
- Create: `Runtime/Controller/UITransitionResult.cs`
- Modify: `Runtime/IUIService.cs`
- Modify: `Runtime/AppUIManager.cs`
- Modify: `Runtime/Operation/UIOperationTypes.cs`
- Modify: `Runtime/Operation/UIOperationCoordinator.cs`
- Modify: `Runtime/Strategy/IUILoadStrategy.cs`
- Modify: `Runtime/Strategy/DefaultUILoadStrategy.cs`
- Modify: `Runtime/Result/UIResultTypes.cs`
- Modify: `Runtime/Controller/UIBaseController.cs`
- Modify: `Tests/Runtime/AppUIRuntimeBoundaryTests.cs`
- Delete: `Runtime/AssetLoading/ResourcesUIAssetProvider.cs`

**Interfaces:**
- Consumes: injected operation factory, execution context and provider.
- Produces: neutral `IUIService` methods, provider `Load<T>`, controller `UITransition`, callback-driven Open/Refresh/Close lifecycle.

- [ ] **Step 1: Rewrite one lifecycle integration test to the desired API and verify RED**

Replace the UniTask wrapper in `PageLifecycle_OpenRefreshHideReopenAndRelease_IsComplete` with a manual completion helper:

```csharp
private static IEnumerator WaitFor<TResult>(
    IUIOperation<TResult> operation,
    Action<AppUIOperationCompletion<TResult>> assertCompletion)
{
    while (!operation.IsTerminal)
    {
        yield return null;
    }

    Assert.That(operation.TryGetCompletion(out AppUIOperationCompletion<TResult> completion),
        Is.True);
    assertCompletion(completion);
}
```

Call `fixture.Manager.Open(...)`, `Refresh(...)`, `Close(...)` and assert `completion.Status == Succeeded` before asserting the existing domain result. Run the single PlayMode test. Expected RED: the non-Async methods do not exist.

- [ ] **Step 2: Add provider and transition contract tests**

Add tests proving:

- `TryLoad` unsupported is an explicit `SynchronousLoadUnsupported` result;
- provider `Load<T>` receives the Operation cancellation token;
- `UITransition.Immediate` does not allocate or request an Operation;
- `UITransition.WaitFor(null)` throws `ArgumentNullException`;
- a late successful asset load after manager Shutdown disposes its Lease exactly once.

- [ ] **Step 3: Move asset contracts to Core and change Provider signature**

The new Provider surface is:

```csharp
public interface IUIAssetProvider
{
    bool TryLoad<T>(string assetId, out UIAssetLoadResult<T> result)
        where T : UnityEngine.Object;

    IUIOperation<UIAssetLoadResult<T>> Load<T>(
        string assetId,
        CancellationToken cancellationToken)
        where T : UnityEngine.Object;
}
```

Delete `ResourcesUIAssetProvider` and its meta after confirming no runtime references remain.

- [ ] **Step 4: Replace public service API atomically**

`IUIService` and `IUIControllerService` use these names and return types:

```csharp
IUIOperation<UIOpenResult> Open(string pageId);
IUIOperation<UIOpenResult> Open(string pageId, object data);
IUIOperation<UIOpenResult> Open(string pageId, UIOpenArgs args);
IUIOperation<UICloseResult> Close(string pageId);
IUIOperation<UICloseResult> CloseTop();
IUIOperation<UICloseResult> CloseTop(UILayerId layerId);
IUIOperation<UIRefreshResult> Refresh(string pageId, object data);
IUIOperation<UIRefreshResult> Refresh(string pageId, UIRefreshArgs args);
IUIOperation<UICancelResult> Cancel();
IUIOperation<UISceneBindResult> BindScene(SceneUIBindingData bindingData);
IUIOperation<UISceneExitResult> UnbindScene(SceneUIBindingData bindingData);
IUIOperation<UIScopeReleaseResult> ReleaseScope(UIPageScope scope, string sceneScopeId);
```

Add `UISceneBindResult` with success and per-rule open results so `BindScene` has a typed result instead of a resultless task.

- [ ] **Step 5: Convert pending intents to injected Sources**

`UIPendingIntent` stores exactly one typed Source matching its intent. `UIOperationCoordinator.EnqueueOpenPending`, `EnqueueClosePending`, and `EnqueueRefreshPending` accept the manager factory and return the Source.Operation. Replaced/expired intents receive `TrySetExpired()` exactly once.

- [ ] **Step 6: Implement callback-driven Open stages**

Split `AppUIManager.Open` into named synchronous stages:

```csharp
public IUIOperation<UIOpenResult> Open(string pageId, UIOpenArgs args)
{
    IUIOperationSource<UIOpenResult> source = operationFactory.Create<UIOpenResult>(
        AppUIOperationDescriptor.Create("Open:" + pageId, args.CancellationToken));
    BeginOpen(pageId, args, source, runtimeEpoch);
    return source.Operation;
}

private void BeginOpen(...)
private void ContinueOpenAfterLoad(..., AppUIOperationCompletion<UILoadResult> completion)
private void ContinueOpenAfterShow(..., AppUIOperationCompletion<UITransitionResult> completion)
private void CompleteOpen(...)
private void CleanupExpiredOpen(...)
```

Before each state commit check page version, SceneScope, cancellation and runtime epoch. All external completions enter through `UIOperationObserver.Observe(operation, executionContext, continuation)`.

- [ ] **Step 7: Implement callback-driven Refresh, Close, Cancel and pending drain**

Use one source per public call. Close waits for controller hide transition before deactivation/release. Cancel resolves one target, then observes the Close operation and maps its domain result to `UICancelResult`. Pending drain processes one intent at a time and starts the next only from the previous terminal continuation; do not use fire-and-forget.

- [ ] **Step 8: Replace controller async animation hooks**

```csharp
protected virtual UITransition BeginShowTransition()
{
    return UITransition.Immediate;
}

protected virtual UITransition BeginHideTransition()
{
    return UITransition.Immediate;
}
```

`UIBaseController.BeginShow` activates the object and calls `OnBeforeShowEx`; manager either commits `OnShowEx` immediately or observes the supplied Operation. Hide performs `OnBeforeHideEx`, waits if required, then invokes `OnHideEx` and deactivates.

- [ ] **Step 9: Convert load strategy**

`IUILoadStrategy.Load` returns `IUIOperation<UILoadResult>`. `DefaultUILoadStrategy` first uses `TryLoad`; when unsupported it calls Provider `Load`, maps asset result to `UILoadResult`, and completes a Source created by the injected factory. Provider exceptions become Failed Operation; NotFound remains a successful Operation containing a failed domain result.

Use this exact strategy signature:

```csharp
IUIOperation<UILoadResult> Load(
    UIPageDefinition definition,
    IUIAssetProvider assetProvider,
    IUIOperationFactory operationFactory,
    CancellationToken cancellationToken);
```

- [ ] **Step 10: Rewrite all page lifecycle PlayMode tests and verify GREEN**

Replace every `UniTask.ToCoroutine`, `await`, `UniTask.Yield`, and `DisposeAsync` in `AppUIRuntimeBoundaryTests` with the `WaitFor` helper, explicit frame yields, and synchronous fixture shutdown. Preserve all existing assertions for lifecycle counts, cancel routing, late loads, notices, raycast and lease ownership.

- [ ] **Step 11: Run focused PlayMode tests, then full EditMode and PlayMode suites**

Expected: all lifecycle behavior remains green with no UniTask use in the converted files.

- [ ] **Step 12: Commit Task 3**

```powershell
git add Runtime/Core/AssetLoading Runtime/AppUIManager.cs Runtime/IUIService.cs Runtime/Operation Runtime/Controller Runtime/Strategy Tests/Runtime/AppUIRuntimeBoundaryTests.cs
git add -u Runtime/AssetLoading
git commit -m "Migrate AppUI page lifecycle to neutral operations"
```

---

### Task 4: Convert scene, flow, focus, and fire-and-forget call sites

**Files:**
- Modify: `Runtime/SceneBinding/SceneUIBinding.cs`
- Modify: `Runtime/SceneBinding/UISceneScopeCoordinator.cs`
- Modify: `Runtime/Flow/UIFlowTypes.cs`
- Modify: `Runtime/Flow/AppUIFlowCoordinator.cs`
- Modify: `Runtime/Selection/AppUIFocusScrolling.cs`
- Modify: `Runtime/Selection/AppUIFocusScope.cs`
- Modify: `Runtime/Controller/PanelBaseController.cs`
- Modify: `Runtime/Input/UIBackgroundClickHandler.cs`
- Modify: `Tests/Editor/AppUIFocusScrollingTests.cs`
- Create: `Tests/Editor/AppUIFlowOperationTests.cs`
- Create: `Tests/Editor/AppUISceneOperationTests.cs`

**Interfaces:**
- Consumes: Task 3 `IUIService` and Operation protocol.
- Produces: neutral scene/flow/focus contracts and no dropped fire-and-forget failures.

- [ ] **Step 1: Write failing sequential scene-operation tests**

Use manual operations to prove `UnbindScene` closes rules in authored order, continues through domain close failures, and completes only after scope releases. Include a cancellation test where later rules never start.

- [ ] **Step 2: Write failing flow-operation tests**

Cover `OpenPage`, `ReplacePage`, `CloseCurrent`, and `CloseCurrentAndRefreshTarget`. Assert the second operation does not start before the first succeeds and that domain failures map to `UIFlowApplyResult.Failed` without producing an Operation-level Failed status.

- [ ] **Step 3: Run the focused tests and verify RED**

Expected: scene and flow interfaces still expose UniTask methods.

- [ ] **Step 4: Convert scene coordinator to index-driven continuation stages**

Replace loops containing await with methods carrying `(rules, index, accumulator, source, epoch)`. Each observed operation schedules exactly the next index. `SceneUIBinding.Bind` and `Unbind` return neutral Operations.

- [ ] **Step 5: Convert flow contracts and coordinator**

Change:

```csharp
public interface IUILocalizationService
{
    IUIOperation<UIUnit> EnsureReady();
    string Localize(string key);
    bool TryLocalize(string key, out string value);
    string Format(string key, object arg0);
}

public interface IUIFlowCoordinator
{
    IUIOperation<UIFlowApplyResult> Apply(
        string currentPageId,
        UIFlowContextBase context,
        IUIFlowCommandResult result);
}
```

Implement the same index/stage pattern used by scene coordination.

- [ ] **Step 6: Convert focus virtualization**

`IAppUIFocusVirtualizationAdapter.EnsureRealized` returns `IUIOperation<AppUIFocusRealizationResult>`. `AppUIFocusScope` stores and disposes the current subscription, calls `RequestCancellation` when the pending target changes, verifies request version on completion, then commits selection. Remove `UniTaskVoid` and `.Forget()`.

- [ ] **Step 7: Replace fire-and-forget close calls with explicit observation**

`PanelBaseController` and `UIBackgroundClickHandler` call `Close` and register a completion that logs only Operation-level Failed exceptions. Store the returned subscription with existing dispose helpers so callbacks cannot target destroyed objects.

- [ ] **Step 8: Run focused and full test suites**

Expected: no UniTask use remains in scene, flow, focus, controller or input files; all semantic focus/input tests remain green.

- [ ] **Step 9: Commit Task 4**

```powershell
git add Runtime/SceneBinding Runtime/Flow Runtime/Selection/AppUIFocusScrolling.cs Runtime/Selection/AppUIFocusScope.cs Runtime/Controller/PanelBaseController.cs Runtime/Input/UIBackgroundClickHandler.cs Tests/Editor
git commit -m "Migrate AppUI integrations to neutral operations"
```

---

### Task 5: Remove UniTask and Resources from package boundaries

**Files:**
- Create: `Tests/Editor/AppUIAsyncBoundaryTests.cs`
- Modify: `package.json`
- Modify: `Runtime/Joi.H.AppUI.Runtime.asmdef`
- Modify: `Tests/Editor/Joi.H.AppUI.Tests.Editor.asmdef`
- Modify: `Tests/Runtime/Joi.H.AppUI.Tests.Runtime.asmdef`
- Modify: `Samples~/Basic Integration/Joi.H.AppUI.Samples.Basic.asmdef`
- Modify: `D:/UGit/JoiH-AppUI-Lab/UnityTestProject/Packages/manifest.json`
- Delete: `D:/UGit/JoiH-AppUI-Lab/UnityTestProject/Packages/com.cysharp.unitask/`

**Interfaces:**
- Consumes: all migrated Runtime code from Tasks 1–4.
- Produces: a package and consumer project that compile with no UniTask installed.

- [ ] **Step 1: Write failing static boundary tests**

The test locates the package root from `PackageInfo.FindForAssembly`, then asserts:

```csharp
StringAssert.DoesNotContain("com.cysharp.unitask", packageJson);
StringAssert.DoesNotContain("UniTask", runtimeAsmdef);
StringAssert.DoesNotContain("Cysharp.Threading.Tasks", runtimeSources);
StringAssert.DoesNotContain("Resources.Load", runtimeSources);
Assert.That(File.Exists(resourcesProviderPath), Is.False);
```

Also reject Runtime tokens `Task<`, `ValueTask`, `Awaitable`, `IEnumerator` in files that implement waiting behavior. Allow normal synchronous `IEnumerator` only in test code.

- [ ] **Step 2: Run boundary test and verify RED**

Expected: package.json, asmdefs and consumer manifest still contain UniTask.

- [ ] **Step 3: Remove package and asmdef references**

`package.json` dependencies become:

```json
"dependencies": {
  "com.unity.ugui": "2.0.0"
}
```

Remove the UniTask GUID/name from Runtime, Editor test, Runtime test and Sample asmdefs. Remove the consumer manifest dependency and its embedded package folder.

- [ ] **Step 4: Run a clean Unity import without UniTask**

Close any existing Unity instance for the consumer project first. Delete only the consumer project's `Library/ScriptAssemblies` if a stale compiler result prevents reimport; do not delete the full Library unless diagnostics prove it necessary. Launch Unity batch import and capture `neutral-operation-no-unitask-import.log`.

Expected: no package resolution or C# compilation errors.

- [ ] **Step 5: Run boundary, EditMode, and PlayMode suites**

Expected: boundary test passes and all behavior tests pass without UniTask installed.

- [ ] **Step 6: Commit Task 5**

```powershell
git add package.json Runtime Tests Samples~
git commit -m "Remove UniTask and Resources defaults"
```

Do not add the separate consumer project files to the package repository commit.

---

### Task 6: Provide opt-in callback Sample and verify direct integration

**Files:**
- Create: `Samples~/Basic Integration/Callback/CallbackUIOperationFactory.cs`
- Create: `Samples~/Basic Integration/Callback/UnityMainThreadExecutionContext.cs`
- Create: `Samples~/Basic Integration/Callback/InMemoryUIAssetProvider.cs`
- Create: `Samples~/Basic Integration/Tests/CallbackIntegrationTests.cs`
- Create: `Samples~/Basic Integration/Tests/Joi.H.AppUI.Samples.Basic.Tests.asmdef`
- Modify: `Samples~/Basic Integration/SampleAppUIInstaller.cs`
- Modify: `Samples~/Basic Integration/README.md`
- Modify outside repo: imported Sample copy under `D:/UGit/JoiH-AppUI-Lab/UnityTestProject/Assets/Samples/Joi.H AppUI/0.2.0-pre.1/Basic Integration/`

**Interfaces:**
- Consumes: final Core contracts and explicit host initialization.
- Produces: an opt-in, zero-third-party Sample demonstrating one possible implementation without making it Runtime default.

- [ ] **Step 1: Add a failing Sample compile/integration test**

Add the test beside the Sample, import the Sample into the independent consumer project, and construct:

```csharp
CallbackUIOperationFactory factory = new CallbackUIOperationFactory();
UnityMainThreadExecutionContext execution =
    UnityMainThreadExecutionContext.CaptureCurrent();
InMemoryUIAssetProvider assets = new InMemoryUIAssetProvider(factory);
AppUIRuntimeDependencies dependencies =
    new AppUIRuntimeDependencies(factory, assets, execution);
```

Register a prefab by explicit asset ID, initialize the fixture, Open/Refresh/Close through neutral Operations, then assert the page lifecycle and lease release.

- [ ] **Step 2: Run the Sample test and verify RED**

Expected: the imported test assembly fails to compile because callback sample types do not exist.

- [ ] **Step 3: Implement the callback Sample**

The Sample factory implements the same exact-once semantics as the test fake but remains under `Samples~`. It must not use Task, UniTask, Awaitable or IEnumerator. `UnityMainThreadExecutionContext` captures `SynchronizationContext.Current` during installer startup and posts to it. `InMemoryUIAssetProvider` uses an explicit serialized table of `assetId → UnityEngine.Object`; it never calls `Resources.Load`.

- [ ] **Step 4: Make Sample installer the composition root**

`SampleAppUIInstaller.Awake` creates or references the three Sample implementations, calls `runtimeHost.Initialize(dependencies)`, checks `AppUIInitializationResult.Success`, and exposes the resulting `IUIService` only after success. No production type auto-discovers the Sample.

- [ ] **Step 5: Run Sample integration and full suites**

Expected: Sample test passes in a consumer without UniTask; package Runtime remains free of concrete implementations.

- [ ] **Step 6: Commit Task 6**

```powershell
git add Samples~
git commit -m "Add opt-in callback AppUI integration sample"
```

---

### Task 7: Rewrite public documentation and package version

**Files:**
- Modify: `README.md`
- Modify: `Documentation~/index.md`
- Modify: `Documentation~/getting-started.md`
- Modify: `Documentation~/core-concepts.md`
- Modify: `Documentation~/architecture.md`
- Modify: `Documentation~/page-system.md`
- Modify: `Documentation~/lifecycle.md`
- Modify: `Documentation~/faq.md`
- Modify: `Documentation~/integration.md`
- Modify: `Documentation~/validation.md`
- Modify: `Samples~/Basic Integration/README.md`
- Modify: `CHANGELOG.md`
- Modify: `package.json`

**Interfaces:**
- Consumes: final code signatures from Tasks 1–6.
- Produces: public Chinese-first documentation for Unity developers unfamiliar with the original project.

- [ ] **Step 1: Add a failing documentation audit**

Run a PowerShell audit that fails when public docs contain any of:

```text
OpenAsync
CloseAsync
RefreshAsync
UniTask.CompletedTask
ResourcesUIAssetProvider
Use Resources Provider When Missing
InitializeOnAwake
```

The same audit requires `IUIOperation`, `IUIOperationFactory`, `IUIAssetProvider`, `IAppUIExecutionContext`, explicit `Initialize` and the direct Git URL.

- [ ] **Step 2: Verify the documentation audit is RED**

Expected: existing staged public docs still describe UniTask and Resources fallback.

- [ ] **Step 3: Rewrite installation and Getting Started**

Clearly separate:

- install: one Git URL, no third-party dependency;
- integrate: project chooses and implements three interfaces;
- run: explicit initialization, then Open/Register/Refresh/Close;
- optional async syntax: project-owned adapters only.

Use only compilable code copied from the final Sample APIs.

- [ ] **Step 4: Rewrite architecture, concepts, lifecycle, page and FAQ docs**

Explain Operation scheduling state vs domain result, cancellation vs completion, Expired semantics, main-thread marshal, late asset lease cleanup, no fallback, and why Core refuses to choose an async backend.

- [ ] **Step 5: Update package metadata and changelog**

Set `package.json` version to `0.2.0-pre.1`. Add a breaking-change entry documenting removed UniTask API, Resources provider and automatic initialization. Do not claim stable API compatibility.

- [ ] **Step 6: Run link, code-fence, forbidden-term and Sample-snippet audits**

Expected: zero broken local links, balanced fences, zero stale APIs, zero project-specific real names, and all C# snippets compile in the consumer test assembly.

- [ ] **Step 7: Commit Task 7**

```powershell
git add README.md Documentation~ Samples~/Basic\ Integration/README.md CHANGELOG.md package.json
git commit -m "Document dependency-free AppUI integration"
```

---

### Task 8: Full release-candidate verification

**Files:**
- Create outside repo: `D:/UGit/JoiH-AppUI-Lab/extraction/neutral-operation-release-audit.json`
- Create outside repo: Unity test/build logs and XML under `D:/UGit/JoiH-AppUI-Lab/extraction/`
- Modify only when a failing test first reproduces a discovered defect.

**Interfaces:**
- Consumes: complete `0.2.0-pre.1` package.
- Produces: evidence that install, compile, lifecycle, focus/input and build boundaries hold without UniTask.

- [ ] **Step 1: Verify repository static boundaries**

Run:

```powershell
rg -n 'Cysharp\.Threading\.Tasks|UniTask|ResourcesUIAssetProvider|Resources\.Load|OpenAsync|CloseAsync|RefreshAsync' Runtime package.json
```

Expected: no matches. Then run `git diff --check`.

- [ ] **Step 2: Run full EditMode tests**

```powershell
& 'C:\Unity\Unity 6000.0.25f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'D:\UGit\JoiH-AppUI-Lab\UnityTestProject' -runTests -testPlatform EditMode -testResults 'D:\UGit\JoiH-AppUI-Lab\extraction\neutral-operation-editmode.xml' -logFile 'D:\UGit\JoiH-AppUI-Lab\extraction\neutral-operation-editmode.log'
```

Expected: all tests pass, zero compilation errors.

- [ ] **Step 3: Run full PlayMode tests**

Use the same command with `-testPlatform PlayMode`, `neutral-operation-playmode.xml`, and `neutral-operation-playmode.log`. Expected: all tests pass and lifecycle assertions remain intact.

- [ ] **Step 4: Run Mono and IL2CPP player builds**

Use the existing consumer build verification entry points already recorded in `D:/UGit/JoiH-AppUI-Lab/extraction/implementation-report.md`. Capture new logs named `neutral-operation-mono-build.log` and `neutral-operation-il2cpp-build.log`. Each external compile/build wait is capped at 120 seconds; report timeout as an explicit verification boundary.

- [ ] **Step 5: Verify clean direct install**

Create a fresh temporary Unity 6 consumer outside both repositories with only `com.joih.appui`, `com.unity.ugui` and the Unity Test Framework. Confirm Package Manager resolves and scripts compile before adding any host implementation. Then add the callback Sample or equivalent project-owned implementations and verify initialize/open/refresh/close.

- [ ] **Step 6: Write release audit evidence**

`neutral-operation-release-audit.json` records package version, dependency list, forbidden-token counts, EditMode/PlayMode totals, Mono/IL2CPP status, direct-install status, exact log paths and any remaining manual checks.

- [ ] **Step 7: Final local commit if verification produced fixes**

Only if a failing test required code changes, commit the tested fix with its regression test. Otherwise leave the verified task commits unchanged. Do not push until the user reviews the final diff and explicitly authorizes pushing.
