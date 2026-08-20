# Integrating Joi.H AppUI Behavioral Scenarios

Run each prompt in a fresh isolated context. During RED, the agent may inspect
only the listed public AppUI documentation and Samples, must not access the
future `integrating-joih-appui` skill, and must treat the project state as
fictional or disposable. Do not reveal the acceptance rubric to the agent.

## Scenario A: Blank Unity 6 project

### Prompt

> I have a blank Unity 6 project with UGUI. I have not selected an async,
> asset-loading, or text implementation. Install Joi.H AppUI and create the
> smallest running page. Tell me the concrete integration and validation steps
> you would take.

Public evidence packet:

- `README.md`
- `Documentation~/supported-unity-versions.md`
- `Documentation~/getting-started.md`
- `Documentation~/host-integration.md`
- `Documentation~/lifecycle.md`
- `Samples~/Basic Integration/README.md`
- `Samples~/Basic Integration/SampleAppUIInstaller.cs`
- `Samples~/Basic Integration/Callback/CallbackUIOperationFactory.cs`
- `Samples~/Basic Integration/Callback/InMemoryUIAssetProvider.cs`
- `Samples~/Basic Integration/Callback/UnityMainThreadExecutionContext.cs`

### Acceptance rubric

Pass only if the response:

- selects an immutable Tag that the official release table and Release identity
  mark as supported, never `main` or an unversioned Git URL;
- preserves the project's async, asset, and text implementation choices and
  offers the zero-third-party Basic Integration Sample path;
- injects project-owned `IUIOperationFactory`, `IUIAssetProvider`, and
  `IAppUIExecutionContext` implementations; and
- verifies the minimum Open, Refresh, Close, and Shutdown lifecycle loop with
  concrete evidence.

## Scenario B: Existing Addressables and TMP project

### Prompt

> This Unity 6 project already owns Addressables, a main-thread dispatcher, and
> TextMeshPro. Integrate Joi.H AppUI using those existing boundaries and explain
> how you would bring up the first TMP-backed page safely.

Public evidence packet:

- `README.md`
- `Documentation~/host-integration.md`
- `Documentation~/lifecycle.md`
- `Documentation~/binding-workflow.md`
- `Documentation~/textmeshpro-integration.md`
- `Samples~/Custom Host Integration/README.md`
- `Samples~/Custom Host Integration/Runtime/CustomHostAdapters.cs`
- `Samples~/Custom Host Integration/Runtime/CustomHostComposition.cs`
- `Samples~/TextMeshPro Integration/README.md`
- `Samples~/TextMeshPro Integration/Runtime/TextMeshProSampleInstaller.cs`
- `Samples~/TextMeshPro Integration/Runtime/TextMeshProSampleAdapters.cs`

### Acceptance rubric

Pass only if the response:

- adapts the existing Addressables and main-thread boundaries instead of
  replacing them or silently selecting another implementation;
- supplies all three project-owned host ports and closes the Base UGUI runtime
  lifecycle loop before enabling optional text integration; and
- explicitly enables `JOIH_APPUI_TMP`, the `joih.appui.tmp` Binding provider,
  and the TMP focus resolver, then reruns Generate, domain reload, Bind,
  validation, and relevant tests.

## Scenario C: Existing interactive UGUI page

### Prompt

> Migrate an existing interactive UGUI page to Joi.H AppUI. It already has
> business selection, EventSystem focus, hover visuals, world input, and Cancel
> behavior. Describe what you inspect and change, in order, and how you prove
> the migrated page is safe across its full lifecycle.

Public evidence packet:

- `Documentation~/core-concepts.md`
- `Documentation~/page-system.md`
- `Documentation~/binding-workflow.md`
- `Documentation~/focus-system.md`
- `Documentation~/input-policy.md`
- `Documentation~/lifecycle.md`
- `Documentation~/editor-tools-validation.md`
- `Samples~/Custom Host Integration/README.md`
- `Samples~/Custom Host Integration/Runtime/CustomHostComposition.cs`
- `Samples~/Custom Host Integration/Tests/CustomHostContractTests.cs`

### Acceptance rubric

Pass only if the response:

- inspects Controller, Prefab, Binding, Definition, runtime, and input contracts
  in that order before choosing edits;
- keeps business selection, EventSystem focus, hover visuals, world-input
  blocking, and Cancel policy as separate states;
- runs Generate, waits for compilation/domain reload, then runs Bind and
  validation rather than collapsing the Binding stages; and
- verifies scene release as well as ordinary Close, including Open, Refresh,
  Cancel, re-open, and Shutdown where the host lifecycle requires them.

## Scenario D: Version sources disagree

### Prompt

> An older Joi.H AppUI tutorial tells me to install one Tag, but the official
> support table and immutable GitHub Release identify a newer officially
> supported Tag. My project may already contain the older package. Which version
> should I install or keep, and what do you do before editing the integration?

Public evidence packet:

- `README.md`
- `CHANGELOG.md`
- `Documentation~/getting-started.md`
- `Documentation~/supported-unity-versions.md`
- `Documentation~/migration-0.4.md`
- `Documentation~/validation.md`

### Acceptance rubric

Pass only if the response:

- gives precedence to immutable Tag and GitHub Release identity, then the
  official support table, package metadata and migration guide, and only then
  tutorials;
- reports the source mismatch explicitly and refuses to install the stale
  tutorial Tag; and
- inspects the installed package source and version before choosing an upgrade
  or migration path.

## Common failure conditions

- Recommends `main` or an unversioned Git URL.
- Assumes UniTask, Resources, Addressables, or TMP without project choice.
- Treats imported Sample types as Runtime defaults.
- Skips one of the three required host ports.
- Generates and binds without a compilation/domain-reload boundary.
- Uses focus or hover as business selection.
- Claims completion without scene release, Shutdown, or validation evidence.
