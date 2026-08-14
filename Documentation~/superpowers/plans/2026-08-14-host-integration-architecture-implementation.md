# Joi.H AppUI Host Integration Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `v0.3.0-pre.1` with generation-safe scene integration,
ownership-safe instance strategies, deterministic runtime/editor composition,
a reusable conformance test kit, and a complete custom-host sample.

**Architecture:** Keep the three required host ports unchanged. Add a separate
optional runtime configuration phase, internal SceneScope generation stamps,
composite cancellation, an allocation-based instance lifetime boundary, and a
keyed Editor resolver registry selected by project settings. Prove the boundary
through package tests and a real external Consumer.

**Tech Stack:** Unity 6000.0.25f1, C# 9-compatible Unity compiler, UGUI 2.0.0,
Unity Test Framework/NUnit, PowerShell release tools, UPM Git installation.

## Global Constraints

- Official target remains Unity `6000.0`; do not create another official Unity line.
- No UniTask, Task, Awaitable, coroutine backend, Addressables, YooAsset,
  GameFramework, Odin, or Resources runtime provider dependency.
- Runtime must not discover host lifecycle or scan host assemblies.
- Test-only drivers must not create Runtime ports.
- Public release tags are immutable; never move `v0.2.0-pre.4`.
- Use TDD for every production behavior change and record RED before GREEN.
- Do not publish third-party adapter ecosystem claims before LICENSE exists.

---

### Task 1: Freeze the host integration specification

**Files:**
- Create: `Documentation~/superpowers/specs/2026-08-14-host-integration-architecture-design.md`
- Create: `Documentation~/superpowers/plans/2026-08-14-host-integration-architecture-implementation.md`

**Interfaces:**
- Consumes: current `v0.2.0-pre.4` public ports and lifecycle APIs.
- Produces: authority, ownership, cancellation, generation, configuration, and
  acceptance rules used by every later task.

- [x] **Step 1: Verify the current worktree is isolated and clean**
- [x] **Step 2: Run the 25 release-tool baseline tests**
- [x] **Step 3: Write the confirmed design specification**
- [x] **Step 4: Write this executable plan**
- [x] **Step 5: Run placeholder and consistency scans**

Run:

```powershell
rg -n 'TBD|TODO|FIXME|similar to|implement later' `
  Documentation~/superpowers/specs/2026-08-14-host-integration-architecture-design.md `
  Documentation~/superpowers/plans/2026-08-14-host-integration-architecture-implementation.md
git diff --check
```

Expected: no placeholder hit and no whitespace error.

### Task 2: Add SceneScope generation and pending-open invalidation

**Files:**
- Create: `Runtime/SceneBinding/UISceneScopeGeneration.cs`
- Create: `Runtime/SceneBinding/UISceneScopeGenerationRegistry.cs`
- Modify: `Runtime/Args/UIOpenArgs.cs`
- Modify: `Runtime/Args/UICloseRequest.cs`
- Modify: `Runtime/Operation/UIOperationTypes.cs`
- Modify: `Runtime/Operation/UIOperationCoordinator.cs`
- Modify: `Runtime/Instance/UIPageInstance.cs`
- Modify: `Runtime/SceneBinding/UISceneScopeCoordinator.cs`
- Modify: `Runtime/AppUIManager.cs`
- Modify: `Runtime/AppUIManager.PageOperations.cs`
- Test: `Tests/Editor/AppUISceneOperationTests.cs`
- Test: `Tests/Runtime/AppUIRuntimeBoundaryTests.cs`

**Interfaces:**
- Consumes: `SceneScopeId`, page operation version, pending-intent cancellation.
- Produces: internal `UISceneScopeStamp`, exact-generation instance ownership,
  and synchronous invalidation on Unbind/Release.

- [x] **Step 1: Write failing generation tests**

Add tests named:

```csharp
SceneScopeGeneration_InvalidateThenActivate_ReturnsNewStamp
UnbindThenRebindSameScope_OldLateLoadCannotCommit
ReleaseScopeThenRebind_OldGenerationCannotCommit
```

- [x] **Step 2: Run the focused tests and verify RED**

Run the Unity EditMode/PlayMode test filters for the three names. Expected:
pending load commits or generation API is missing.

- [x] **Step 3: Implement `UISceneScopeStamp` and registry**

Required internal shape:

```csharp
internal readonly struct UISceneScopeStamp
{
    public string SceneScopeId { get; }
    public int Generation { get; }
}

internal sealed class UISceneScopeGenerationRegistry
{
    public UISceneScopeStamp Begin(string sceneScopeId);
    public UISceneScopeStamp Invalidate(string sceneScopeId);
    public UISceneScopeStamp GetCurrent(string sceneScopeId);
    public bool IsCurrent(UISceneScopeStamp stamp);
    public void Clear();
}
```

- [x] **Step 4: Stamp active/pending opens and instances**

Add internal generation data to immutable args, page operations, pending intent,
and `UIPageInstance`. Existing public constructors keep generation hidden.

- [x] **Step 5: Invalidate before Unbind/Release work is built**

Cancellation predicates and close work must compare both scope ID and retiring
generation. Global pages ignore the stamp.

- [x] **Step 6: Run focused tests and verify GREEN**
- [x] **Step 7: Run all package EditMode and PlayMode tests**
- [x] **Step 8: Commit the generation change**

### Task 3: Add composite-operation cancellation

**Files:**
- Create: `Runtime/Operation/UICompositeOperationState.cs`
- Modify: `Runtime/SceneBinding/UISceneScopeCoordinator.cs`
- Test: `Tests/Editor/AppUISceneOperationTests.cs`

**Interfaces:**
- Consumes: `IUIOperation<T>.RequestCancellation`, `CancellationToken`, and
  `IAppUIExecutionContext`.
- Produces: a testable internal composite state that owns one child subscription
  and propagates outer cancellation.

- [x] **Step 1: Write failing cancellation tests**

```csharp
BindScene_OuterCancellation_CancelsCurrentOpenAndSkipsRemainingRules
UnbindScene_OuterCancellation_CancelsCurrentCloseAndSkipsRemainingRules
ReleaseScope_LateChildCompletionAfterCancellation_DoesNotChangeOuterTerminal
```

- [x] **Step 2: Run focused tests and verify RED**
- [x] **Step 3: Implement the minimal composite state**

The state registers the outer token, tracks the current child operation and
subscription, calls `RequestCancellation`, prevents later work, and disposes all
registrations exactly once.

- [x] **Step 4: Route scene bind/unbind/release sequences through the state**
- [x] **Step 5: Run focused tests and verify GREEN**
- [x] **Step 6: Run the complete package test suites**
- [x] **Step 7: Commit the composite cancellation change**

### Task 4: Replace asymmetric destroy strategy with instance ownership

**Files:**
- Create: `Runtime/Instance/UIAssetLeaseTransfer.cs`
- Create: `Runtime/Instance/UIPageInstanceAllocation.cs`
- Create: `Runtime/Strategy/IUIPageInstanceStrategy.cs`
- Create: `Runtime/Strategy/DefaultUIPageInstanceStrategy.cs`
- Modify: `Runtime/Definition/UIPageDefinition.cs`
- Modify: `Runtime/Instance/UIPageInstance.cs`
- Modify: `Runtime/Instance/UIPageInstanceReleaser.cs`
- Modify: `Runtime/AppUIManager.cs`
- Modify: `Runtime/AppUIManager.PageOperations.cs`
- Delete: `Runtime/Strategy/IUIDestroyStrategy.cs`
- Delete: `Runtime/Strategy/DefaultUIDestroyStrategy.cs`
- Test: `Tests/Editor/AppUIAssetBoundaryTests.cs`
- Test: `Tests/Runtime/AppUIRuntimeBoundaryTests.cs`

**Interfaces:**
- Consumes: loaded prefab and `UIAssetLease`.
- Produces: `IUIPageInstanceStrategy`, one-shot lease transfer, and allocation
  release as the only instance/asset release boundary.

- [x] **Step 1: Write failing transfer/allocation tests**

```csharp
LeaseTransfer_Claim_MovesOwnershipExactlyOnce
DefaultInstanceAllocation_Release_DestroysAndDisposesLeaseOnce
CreateFailure_UnclaimedLeaseReturnsToAppUI
PoolingStrategy_ReturnKeepsLeaseUntilPoolEviction
```

- [x] **Step 2: Run tests and verify RED**
- [x] **Step 3: Implement one-shot transfer and allocation**

The allocation exposes the GameObject and owns an idempotent release callback.
The transfer releases an unclaimed lease on dispose; claiming is atomic.

- [x] **Step 4: Introduce `IUIPageInstanceStrategy`**

Do not expose AppUI internals. The creation request contains definition, prefab,
parent, and the lease transfer. A valid result must claim ownership.

- [x] **Step 5: Replace `DestroyStrategyId` with `InstanceStrategyId`**
- [x] **Step 6: Make all open-failure and normal-release paths dispose allocation**
- [x] **Step 7: Run focused and complete tests**
- [x] **Step 8: Commit the instance ownership change**

### Task 5: Add deterministic optional Runtime Configuration

**Files:**
- Create: `Runtime/Bootstrap/AppUIRuntimeConfiguration.cs`
- Modify: `Runtime/Bootstrap/AppUIRuntimeDependencies.cs`
- Modify: `Runtime/Bootstrap/AppUIRuntimeHost.cs`
- Modify: `Runtime/Bootstrap/AppUIInitializationResult.cs`
- Modify: `Runtime/AppUIManager.cs`
- Test: `Tests/Editor/AppUIRuntimeHostTests.cs`

**Interfaces:**
- Consumes: load and instance strategies.
- Produces: `Initialize(dependencies, configuration)` and structured duplicate /
  unknown strategy failures before runtime readiness.

- [x] **Step 1: Write failing initialization tests**

```csharp
Initialize_ConfigurationInstallsStrategiesBeforeDefinitionValidation
Initialize_DuplicateStrategyId_ReturnsStructuredFailure
Initialize_UnknownDefinitionStrategy_ReturnsStructuredFailureInReleasePolicy
ShutdownThenInitialize_ReappliesNewConfiguration
```

- [x] **Step 2: Run tests and verify RED**
- [x] **Step 3: Implement immutable configuration snapshot**
- [x] **Step 4: Add the two-argument Initialize overload**
- [x] **Step 5: Make manager validation return structured failures**
- [x] **Step 6: Run focused and complete tests**
- [x] **Step 7: Commit the configuration change**

### Task 6: Make Editor AssetId authoring explicit and deterministic

**Files:**
- Modify: `Editor/Binding/Resolver/IUIEditorAssetIdResolver.cs`
- Modify: `Editor/Binding/Settings/UIBindingSettings.cs`
- Modify: `Editor/Binding/Settings/UIBindingSettingsProvider.cs`
- Modify every resolver consumer under: `Editor/Binding/**`
- Test: `Tests/Editor/AppUIEditorAssetIdResolverTests.cs`

**Interfaces:**
- Consumes: `IUIEditorAssetIdResolver`.
- Produces: keyed registry, project-selected resolver ID, centralized Missing and
  duplicate diagnostics, and no implicit Resources default.

- [x] **Step 1: Write failing registry/selection tests**

```csharp
Registry_MissingSelection_ReturnsConfiguredDiagnostic
Registry_DuplicateId_IsRejectedWithoutReplacingOriginal
Settings_SelectedResolverId_ResolvesDeterministically
Registry_DoesNotInstallResourcesImplicitly
```

- [x] **Step 2: Run tests and verify RED**
- [x] **Step 3: Implement keyed resolver registration**
- [x] **Step 4: Add `SelectedAssetIdResolverId` to settings**
- [x] **Step 5: Route all Editor tools through one resolver service**
- [x] **Step 6: Add Project Settings navigation for interactive errors**
- [x] **Step 7: Run Editor and Binding tests**
- [x] **Step 8: Commit the Editor authoring change**

### Task 7: Publish an optional Host Integration Contract Test Kit

**Files:**
- Create: `Tests/HostIntegration/**`
- Create: `Tests/HostIntegration/Joi.H.AppUI.Tests.HostIntegration.asmdef`
- Modify: `package.json`
- Test: official Consumer adapter tests inherit the new fixtures.

**Interfaces:**
- Consumes: only public AppUI contracts plus test-only drivers.
- Produces: public abstract NUnit fixtures for Operation, Asset, Execution,
  Lifecycle, and Instance conformance.

- [x] **Step 1: Write the abstract fixture API and adapt one official Consumer test**
- [x] **Step 2: Verify the adapted test fails before fixture implementation**
- [x] **Step 3: Implement Operation/Asset/Execution fixtures**
- [x] **Step 4: Implement Lifecycle/Instance drivers and fixtures**
- [x] **Step 5: Confirm the test assembly is excluded from Player builds**
- [x] **Step 6: Run official Consumer contract tests**
- [x] **Step 7: Commit the conformance test kit**

### Task 8: Add Custom Host Integration Sample

**Files:**
- Create: `Samples~/Custom Host Integration/**`
- Modify: `package.json`
- Modify: `Samples~/Basic Integration/**` for explicit sample resolver only.

**Interfaces:**
- Consumes: three required ports, optional configuration, lifecycle APIs, input
  hit resolver, and the conformance test kit.
- Produces: complete installer, scene bridge, shutdown flow, optional instance
  strategy, Editor resolver, and test driver without third-party names.

- [ ] **Step 1: Add sample tests that fail because the sample is absent**
- [ ] **Step 2: Implement the sample composition root and lifecycle bridge**
- [ ] **Step 3: Implement explicit sample Editor resolver registration**
- [ ] **Step 4: Implement sample contract-test drivers**
- [ ] **Step 5: Import the sample into a clean Consumer and run tests**
- [ ] **Step 6: Commit the custom-host sample**

### Task 9: Synchronize public docs and migrate to 0.3

**Files:**
- Create: `Documentation~/host-integration.md`
- Create: `Documentation~/migration-0.3.md`
- Modify: `README.md`
- Modify: `Documentation~/index.md`
- Modify: `Documentation~/architecture.md`
- Modify: `Documentation~/integration.md`
- Modify: `Documentation~/lifecycle.md`
- Modify: `Documentation~/binding-system.md`
- Modify: `Documentation~/faq.md`
- Modify: `Documentation~/validation.md`
- Modify: `CHANGELOG.md`
- Modify: `package.json`

**Interfaces:**
- Consumes: final public 0.3 API.
- Produces: one consistent external integration story and migration guide.

- [ ] **Step 1: Set package version to `0.3.0-pre.1`**
- [ ] **Step 2: Document authority, ownership, cancellation, configuration, and
  Editor resolver rules**
- [ ] **Step 3: Document `DestroyStrategyId -> InstanceStrategyId` migration**
- [ ] **Step 4: Document LICENSE as the P2 ecosystem gate**
- [ ] **Step 5: Run stale-token and public-doc consistency scans**
- [ ] **Step 6: Commit the documentation/version migration**

### Task 10: Full external validation and immutable pre-release

**Files:**
- Modify only release evidence/docs if generated evidence requires tracked
  updates; generated Consumer/run roots stay outside the repository.

**Interfaces:**
- Consumes: committed `0.3.0-pre.1` candidate.
- Produces: clean package, Consumer, build, Git install, formal artifact, and
  GitHub Pre-release evidence.

- [ ] **Step 1: Run all release-tool tests**
- [ ] **Step 2: Run full PreTag validation from a committed candidate**
- [ ] **Step 3: Verify Binding, EditMode, PlayMode, Mono, and IL2CPP evidence**
- [ ] **Step 4: Push candidate to `origin/main` only after local gates pass**
- [ ] **Step 5: Run exact-commit Git install smoke**
- [ ] **Step 6: Create and push immutable `v0.3.0-pre.1`**
- [ ] **Step 7: Run Tag URL Git install smoke**
- [ ] **Step 8: Stage exactly ten sanitized formal artifacts and verify SHA-256**
- [ ] **Step 9: Create GitHub Pre-release and audit remote asset digests**
- [ ] **Step 10: Update released-state docs on `main` and run final immutable audit**

## Self-review checklist

- [ ] Every design requirement maps to a task.
- [ ] No placeholder or unspecified implementation step remains.
- [ ] Later tasks use the exact types introduced by earlier tasks.
- [ ] No production dependency names a third-party host/backend.
- [ ] No Runtime lifecycle port was added solely for testing.
- [ ] P2 ecosystem claims remain blocked by missing LICENSE.
