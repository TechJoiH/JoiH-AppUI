# Integrating Joi.H AppUI Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, behavior-test and publish a public Codex skill that inspects a Unity project and guides or implements a project-owned Joi.H AppUI installation, host integration, first page, migration and validation workflow.

**Architecture:** Store the public skill in `Skills~/integrating-joih-appui` so Unity ignores it while Git and Skill Installer can distribute it. A read-only PowerShell inventory establishes the current Unity/AppUI state, then a concise route-map skill loads only the installation, host-boundary, runtime, UI-production or troubleshooting reference needed for the request. The workflow reuses official Samples and Editor tools, derives implementation choices from the consumer project, and never turns optional dependencies into Core defaults.

**Tech Stack:** Codex Agent Skills, Markdown, PowerShell 7/Windows PowerShell-compatible scripts, Unity 6 UPM, UGUI, optional TextMeshPro integration, Joi.H AppUI public APIs and Samples.

**Spec:** `Documentation~/superpowers/specs/2026-08-20-joih-appui-codex-skills-design.md`

## Global Constraints

- Complete and validate `maintaining-joih-appui` before executing this plan.
- The public skill name is exactly `integrating-joih-appui`.
- The source root is exactly `Skills~/integrating-joih-appui` in the AppUI package repository.
- `Skills~` contains no Unity `.meta` files because Unity ignores every directory ending in `~`.
- The skill supports official AppUI `0.4.x` contracts first and declares that compatibility explicitly; it must inspect installed version/migration docs before editing a different version.
- Installation uses an immutable Officially Supported Tag, never mutable `main`.
- The skill may not silently install or choose UniTask, Resources, Addressables, TextMeshPro or another implementation.
- Project-owned adapters implement `IUIOperationFactory`, `IUIAssetProvider` and `IAppUIExecutionContext`.
- Base integration is UGUI-only. `JOIH_APPUI_TMP` and TMP-specific providers/resolvers are explicit opt-in work.
- The public skill never exposes or invokes maintainer Push, Tag, GitHub Release or release-artifact workflows.
- Preserve existing project architecture and unrelated changes; follow target-repository rules before editing Unity-managed assets.
- Use `apply_patch` for authored text/code files and test every script against disposable fixtures.

---

### Task 1: Establish public-skill behavioral RED scenarios

**Files:**
- Create: `Skills~/integrating-joih-appui/tests/behavioral-scenarios.md`
- Create after baseline runs: `Skills~/integrating-joih-appui/tests/baseline-findings.md`

**Interfaces:**
- Consumes: The approved design spec, current public AppUI docs/Samples, and fresh agents without the skill.
- Produces: Four scenario prompts and acceptance rubrics reused in Task 8.

- [ ] **Step 1: Write scenarios before `SKILL.md` or scripts exist**

Use `apply_patch` to create:

```markdown
# Integrating Joi.H AppUI Behavioral Scenarios

## Scenario A: Blank Unity 6 project
The project has UGUI but no selected async, asset or text implementation. The
user asks AI to install AppUI and create the smallest running page.

Pass only if AI chooses an immutable official Tag, preserves implementation
choice, offers the zero-third-party Sample path, injects all three host ports,
and verifies Open/Refresh/Close/Shutdown.

## Scenario B: Existing Addressables and TMP project
The project already owns Addressables, a main-thread dispatcher and TMP.

Pass only if AI adapts existing boundaries, closes the Base runtime loop first,
then explicitly enables `JOIH_APPUI_TMP`, Binding provider and focus resolver.

## Scenario C: Existing interactive UGUI page
The page has business selection, EventSystem focus, hover visuals, world input
and Cancel behavior.

Pass only if AI inspects Controller/Prefab/Binding/Definition/runtime/input in
order, keeps interaction states separate, uses Generate then Bind, and validates
scene release as well as ordinary Close.

## Scenario D: Version sources disagree
An old tutorial shows one Tag while the release table and immutable GitHub
Release show a newer officially supported Tag.

Pass only if AI follows immutable release identity and the support table, reports
the mismatch, and refuses the stale tutorial Tag.

## Common failure conditions
- Recommends `main` or an unversioned Git URL.
- Assumes UniTask, Resources, Addressables or TMP without project choice.
- Treats imported Sample types as Runtime defaults.
- Skips one of the three required host ports.
- Generates and binds without a compilation/domain-reload boundary.
- Uses focus or hover as business selection.
- Claims completion without scene release, Shutdown or validation evidence.
```

- [ ] **Step 2: Run four fresh-agent baselines without the skill**

Dispatch one isolated read-only agent per scenario. Give each only public AppUI
documentation relevant to the prompt and explicitly deny access to the new skill.
Use fictional or disposable project state; no real project mutation.

Expected: at least one unsafe assumption or omitted contract is observed. If a
scenario passes completely, keep it as regression coverage but do not add
guidance that merely repeats the baseline agent's correct behavior.

- [ ] **Step 3: Record baseline evidence**

Write `tests/baseline-findings.md` with Scenario, observed behavior, violated
rubric and the smallest guidance needed. Quote only the decisive sentence, not
full transcripts.

- [ ] **Step 4: Commit RED behavior tests**

```powershell
git add -- 'Skills~/integrating-joih-appui/tests'
git commit -m "test: define AppUI integration skill behavior"
```

Expected: no public `SKILL.md` or implementation script exists in this commit.

---

### Task 2: Repair the public installation source-of-truth drift with TDD

**Files:**
- Modify: `Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1`
- Modify: `Documentation~/getting-started.md`

**Interfaces:**
- Consumes: Published `v0.4.0-pre.1` identity and current README/support table.
- Produces: One consistent public install Tag and migration path for the skill to follow.

- [ ] **Step 1: Add failing documentation assertions**

In the existing `Public docs match release tools and planned package version`
test, load `Documentation~/getting-started.md` and assert:

```powershell
Assert-True ($gettingStarted.Contains('https://github.com/TechJoiH/JoiH-AppUI.git#v0.4.0-pre.1')) `
    'Getting Started does not show the current immutable Tag.'
Assert-True ($gettingStarted.Contains('migration-0.4.md')) `
    'Getting Started does not link the current migration guide.'
Assert-True (-not $gettingStarted.Contains('git#main')) `
    'Getting Started recommends main as a production dependency.'
```

- [ ] **Step 2: Run the Docs test and verify RED**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  '.\Tools~\Release\Tests\Invoke-AppUIReleaseToolsTests.ps1' -Group Docs
```

Expected: FAIL because Getting Started still points at `v0.3.0-pre.1` and the 0.3 migration guide.

- [ ] **Step 3: Update Getting Started to the published release**

Replace the install URL and release link with `v0.4.0-pre.1`, describe the Base
and optional TMP boundaries, and direct 0.3 users to `migration-0.4.md`. Preserve
older releases only as historical support-table entries, not the recommended URL.

- [ ] **Step 4: Run Docs tests and verify GREEN**

Use the Step 2 command. Expected: Docs group passes.

- [ ] **Step 5: Commit the documentation repair**

```powershell
git add -- Documentation~/getting-started.md Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1
git commit -m "docs: align AppUI getting started with 0.4 release"
```

---

### Task 3: Scaffold the public skill and UI metadata

**Files:**
- Create: `Skills~/integrating-joih-appui/SKILL.md`
- Create: `Skills~/integrating-joih-appui/agents/openai.yaml`
- Create directories: `scripts/`, `references/`
- Preserve: `tests/` from Task 1

**Interfaces:**
- Consumes: Task 1 baseline failures.
- Produces: A discoverable public skill with automatic invocation enabled.

- [ ] **Step 1: Initialize into a temporary directory**

The standard initializer refuses an existing Task 1 directory, so scaffold into
a new temporary path outside the repository:

```powershell
$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$staging = Join-Path $tempRoot ("integrating-joih-appui-skill-scaffold-" + [guid]::NewGuid().ToString('N'))
python 'C:\Users\HorizonEdge_00006\.codex\skills\.system\skill-creator\scripts\init_skill.py' `
  integrating-joih-appui --path $staging --resources scripts,references `
  --interface 'display_name=Integrate Joi.H AppUI' `
  --interface 'short_description=Inspect and integrate AppUI into a Unity project' `
  --interface 'default_prompt=Use $integrating-joih-appui to inspect this Unity project and build the next safe AppUI integration step.'
```

- [ ] **Step 2: Move only scaffolded production files into the existing test root**

Resolve both paths, verify the staging root is a strict descendant of
`$tempRoot`, and verify the destination contains only `tests/`. Move `SKILL.md`,
`agents`, `scripts` and `references` into `Skills~/integrating-joih-appui`, then
delete only that verified unique staging directory.

- [ ] **Step 3: Set final frontmatter**

```yaml
---
name: integrating-joih-appui
description: Use when installing, integrating, learning, migrating, or diagnosing Joi.H AppUI in a Unity project, including host adapters, pages, Binding, Focus, Input, lifecycle, or optional TextMeshPro.
---
```

- [ ] **Step 4: Normalize UI metadata**

```yaml
interface:
  display_name: "Integrate Joi.H AppUI"
  short_description: "Inspect and integrate AppUI into a Unity project"
  default_prompt: "Use $integrating-joih-appui to inspect this Unity project and build the next safe AppUI integration step."
policy:
  allow_implicit_invocation: true
```

- [ ] **Step 5: Verify Unity-ignore layout**

Assert that every path under `Skills~` has no `.meta` companion and that the
existing package policy still accepts a trailing-tilde folder. Do not add
`Skills~.meta` or child `.meta` files.

---

### Task 4: Implement the Unity/AppUI project inspector with TDD

**Files:**
- Create: `Skills~/integrating-joih-appui/tests/Invoke-IntegratingAppUISkillTests.ps1`
- Create: `Skills~/integrating-joih-appui/scripts/inspect-appui-project.ps1`

**Interfaces:**
- Produces command:
  `inspect-appui-project.ps1 -ProjectPath string [-OutputPath string] [-MaxSourceFiles 2000]`
- Produces JSON schema `joih-appui-project-inspection.v1` with `status`, `project`, `packages`, `integration`, `samples`, and `issues`.

- [ ] **Step 1: Write failing disposable-project tests**

The harness creates exact fake Unity roots with `Assets`, `Packages`,
`ProjectSettings/ProjectVersion.txt`, `manifest.json` and `packages-lock.json`.
Assert these cases:

```powershell
Assert-Equal 'NotAUnityProject' (Inspect $ordinaryFolder).status
Assert-Equal 'AppUINotInstalled' (Inspect $unityWithoutAppUI).status
Assert-Equal 'InstalledNotInitialized' (Inspect $appUIManifestOnly).status
Assert-Equal 'HostBoundariesMissing' (Inspect $runtimeHostWithoutPorts).status
Assert-Equal 'RuntimeRootIncomplete' (Inspect $portsWithoutProfileOrLayer).status
Assert-Equal 'PageContractIncomplete' (Inspect $runtimeWithoutPageContract).status
Assert-Equal 'BindingGenerationPending' (Inspect $pageWithoutBindings).status
Assert-Equal 'Ready' (Inspect $completeFixture).status
```

Also assert exact Git Tag parsing, Unity version, UGUI/TMP package facts,
`JOIH_APPUI_TMP` detection, imported Sample paths, and that a sentinel stored in
`.env` never appears in JSON.

- [ ] **Step 2: Run tests and verify RED**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  '.\Skills~\integrating-joih-appui\tests\Invoke-IntegratingAppUISkillTests.ps1'
```

Expected: FAIL because `inspect-appui-project.ps1` does not exist.

- [ ] **Step 3: Implement root and package discovery**

The script starts with:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$OutputPath = '',
    [ValidateRange(1, 10000)][int]$MaxSourceFiles = 2000
)
```

Walk parents until all three Unity markers exist. Parse JSON with UTF-8, parse
`m_EditorVersion` from `ProjectVersion.txt`, and return manifest plus lock-file
facts for AppUI, UGUI, TMP and detected async/asset candidates. Mark branch or
unversioned AppUI Git references as mutable; do not call the network.

- [ ] **Step 4: Implement bounded integration discovery**

Scan only `.cs`, `.asmdef` and selected `.asset` filenames under `Assets`, stop at
`MaxSourceFiles`, and exclude hidden/secret files. Detect likely implementations
of the three ports, `AppUIRuntimeHost`, layer/profile/registry/binding assets,
generated `.Bindings.cs`, page controllers and imported Samples. The inspector
reports candidates and confidence; it does not claim semantic correctness from
text search alone.

Resolve status with this precedence:

```powershell
NotAUnityProject -> UnityVersionUnverified -> AppUINotInstalled ->
InstalledNotInitialized -> HostBoundariesMissing -> RuntimeRootIncomplete ->
PageContractIncomplete -> BindingGenerationPending -> BindingInvalid ->
RuntimeValidationPending -> Ready
```

`BindingInvalid` and `RuntimeValidationPending` require discoverable validation
evidence; absence of evidence is never converted to Passed.

- [ ] **Step 5: Run tests and verify GREEN**

Use the Step 2 command. Expected: all fixtures pass, `.env` sentinel is absent,
and the temporary root is deleted after boundary checks.

- [ ] **Step 6: Commit the inspector**

```powershell
git add -- 'Skills~/integrating-joih-appui/scripts' 'Skills~/integrating-joih-appui/tests'
git commit -m "feat: inspect Unity projects for AppUI integration"
```

---

### Task 5: Write installation, host-boundary and runtime references

**Files:**
- Create: `Skills~/integrating-joih-appui/references/installation.md`
- Create: `Skills~/integrating-joih-appui/references/host-boundaries.md`
- Create: `Skills~/integrating-joih-appui/references/runtime-root.md`

**Interfaces:**
- Consumes: Inspector report and public AppUI `0.4.x` contracts.
- Produces: The complete path from uninstalled project to initialized Runtime with no business page.

- [ ] **Step 1: Write installation routing**

`installation.md` must require the source-of-truth order:

```text
immutable GitHub Tag/Release
→ supported-unity-versions.md
→ package.json and migration guide at that Tag
→ installed package and Samples
→ tutorials
```

Show Package Manager and `Packages/manifest.json` install forms using the
`$officialTag` value resolved from repository evidence (currently
`v0.4.0-pre.1`). Reject `main`, unversioned URLs and
Known Incompatible combinations. Distinguish Officially Supported, Community
Verified, Community Port, Unsupported and Known Incompatible.

- [ ] **Step 2: Write the host-boundary decision table**

`host-boundaries.md` explains exact responsibilities and ownership for:

```csharp
IUIOperationFactory operations = projectOperations;
IUIAssetProvider assets = projectAssets;
IAppUIExecutionContext execution = projectExecutionContext;
runtimeHost.Initialize(new AppUIRuntimeDependencies(operations, assets, execution));
```

Map detected callback/Task/Awaitable/coroutine, Addressables/AssetBundle/custom
asset systems and main-thread dispatchers to adapters without prescribing one.
Explain `UIAssetLease` idempotence, late-result cleanup, execution-context commit
and subscription disposal. Route beginners to Basic Integration and production
hosts to Custom Host Integration/contract tests.

- [ ] **Step 3: Write Runtime Root and Shutdown workflow**

`runtime-root.md` covers EventSystem, `AppUIManager`, `AppUIRuntimeHost`,
`UILayerRoot`, CanvasDomain, Registry and Runtime Profile. Include explicit
initialization-result handling and this shutdown order:

```text
stop new UI requests
→ ReleaseScope or UnbindScene
→ AppUIRuntimeHost.Shutdown
→ evict project pools and return Leases
→ stop asset provider
→ destroy project-owned UI root
```

Do not imply that AppUI creates scene roots or guesses lifecycle automatically.

- [ ] **Step 4: Add reference-link tests**

Extend `Invoke-IntegratingAppUISkillTests.ps1` to parse links from `SKILL.md` and
assert every referenced file exists. At this task stage the three new references
pass while still-unwritten later references are not linked from `SKILL.md` yet.

- [ ] **Step 5: Commit the core onboarding references**

```powershell
git add -- 'Skills~/integrating-joih-appui/references' 'Skills~/integrating-joih-appui/tests'
git commit -m "docs: add AppUI host integration workflow"
```

---

### Task 6: Write the reusable UI production workflow

**Files:**
- Create: `Skills~/integrating-joih-appui/references/page-production.md`
- Create: `Skills~/integrating-joih-appui/references/binding-focus-input.md`

**Interfaces:**
- Consumes: Initialized Runtime from Task 5 and the generic production pattern from the local `annals-unity-ui-workflow`.
- Produces: First-page and existing-UGUI migration workflows without Annals-specific types or paths.

- [ ] **Step 1: Write the page contract and investigation order**

`page-production.md` must use this generic order:

```text
host composition and existing UI ownership
→ Controller and business/view state
→ authored Prefab and interaction contract
→ generated Binding, Definition and Registry
→ Runtime open path and Scope
→ Focus/Input/Cancel/world-input chain
→ final data/localization/visual acceptance
```

Define PageId, PrefabAssetId, Layer, CanvasDomain, Scope, OpenPolicy, Cancel,
input blocking and focus before editing. Keep exactly one primary
`PanelBaseController`; create Controller before binding-heavy Prefab work; bind
only runtime-needed `B_` nodes; register Definition explicitly.

- [ ] **Step 2: Write the minimum page lifecycle example**

Include `PanelBaseController` examples for `OnDataLoadEx`, `OnRefreshEx`,
`OnInitEx`, `RegisterDisposeAction`, close eligibility and project-owned
transitions. Include Open, Refresh and Close code that checks both Operation
status and result success, and uses an explicit SceneScopeId when appropriate.

- [ ] **Step 3: Write Binding, Focus and Input gates**

`binding-focus-input.md` requires:

```text
Generate → wait for compile/domain reload → Bind → Validate
```

Explain selected Editor AssetId Resolver, optional Binding Providers, readonly
validators and build gates. Keep business selection, EventSystem focus, hover,
direction chains and Cancel separate. Default focus declares a target; it does
not click/select business data. World actions consult AppUI blocking immediately
before execution rather than toggling arbitrary graphics.

- [ ] **Step 4: Add lifecycle acceptance matrix**

The reference must require evidence for Open, Refresh, Close, Cancel, ReleaseScope,
scene rebind with the same ID, late asset completion, re-open, Shutdown, mouse,
keyboard, controller and intended world-input pass/block behavior. Visual/font/
animation/click-area checks remain manual.

- [ ] **Step 5: Commit the UI production references**

```powershell
git add -- 'Skills~/integrating-joih-appui/references'
git commit -m "docs: add AppUI page production workflow"
```

---

### Task 7: Complete optional integration, migration, troubleshooting and route map

**Files:**
- Create: `Skills~/integrating-joih-appui/references/optional-textmeshpro.md`
- Create: `Skills~/integrating-joih-appui/references/migration.md`
- Create: `Skills~/integrating-joih-appui/references/troubleshooting.md`
- Modify: `Skills~/integrating-joih-appui/SKILL.md`
- Modify: `Skills~/integrating-joih-appui/tests/Invoke-IntegratingAppUISkillTests.ps1`

**Interfaces:**
- Consumes: All earlier references and inspector states.
- Produces: A route-map `SKILL.md` under 500 words with every mode reachable.

- [ ] **Step 1: Write the optional TMP reference**

Document explicit order: close Base runtime loop, add `JOIH_APPUI_TMP` to intended
target, wait for optional assemblies, import Sample if useful, select
`joih.appui.tmp`, inject InputField resolver, configure explicit Dropdown child
regions/Notice Views, then rerun Generate/Bind/Validate/diagnostics/tests/builds.
Installed TMP alone changes nothing.

- [ ] **Step 2: Write migration and troubleshooting references**

`migration.md` first identifies installed source version and reads every migration
guide up to the target Tag. It preserves project adapters and authored assets,
updates breaking contracts deliberately, then reruns full integration evidence.

`troubleshooting.md` maps all inspector states and common errors to evidence-led
checks. It distinguishes compile errors, missing ports, Profile/Layer mismatch,
Definition registration, Provider/Resolver disagreement, Binding compile gap,
Focus versus selection, world-input gating, late loads and shutdown ownership.

- [ ] **Step 3: Write the concise route-map Skill**

`SKILL.md` starts by locating the Unity root and running the inspector. It reports
state/evidence/next action, asks only for missing project choices, and routes:

```markdown
- Install/version issue → `references/installation.md`
- Missing three host ports → `references/host-boundaries.md`
- Runtime root/lifecycle → `references/runtime-root.md`
- First page or legacy migration → `references/page-production.md`
- Binding/Focus/Input/Cancel → `references/binding-focus-input.md`
- Explicit TMP request → `references/optional-textmeshpro.md`
- Upgrade → `references/migration.md`
- Failure diagnosis → `references/troubleshooting.md`
```

Add a status-to-route quick-reference table and non-negotiable rules: no implicit
dependency choice, no mutable install source, no collapsed Binding stages, no
completion claim without lifecycle validation.

- [ ] **Step 4: Complete structural tests**

Assert frontmatter name/description, UI metadata, all links, one inspector script,
no `.meta`, no maintainer `Tools~/Release`/Tag/Release instructions, and no
hardcoded machine paths. Confirm references mention all three ports and all
integration states.

- [ ] **Step 5: Run deterministic GREEN**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  '.\Skills~\integrating-joih-appui\tests\Invoke-IntegratingAppUISkillTests.ps1'
python 'C:\Users\HorizonEdge_00006\.codex\skills\.system\skill-creator\scripts\quick_validate.py' `
  '.\Skills~\integrating-joih-appui'
```

Expected: all fixture, secret-boundary and structure tests pass.

- [ ] **Step 6: Commit the complete public skill**

```powershell
git add -- 'Skills~/integrating-joih-appui'
git commit -m "feat: add Joi.H AppUI integration skill"
```

---

### Task 8: Run behavioral GREEN and focused refactoring

**Files:**
- Create: `Skills~/integrating-joih-appui/tests/green-findings.md`
- Modify only on observed failures: the responsible `SKILL.md` or reference.

**Interfaces:**
- Consumes: Task 1 scenarios and the complete public skill.
- Produces: Independent evidence that AI can apply the workflow safely to new project shapes.

- [ ] **Step 1: Run fresh agents with the public skill**

For each Task 1 scenario, dispatch an isolated agent with:

```text
Use $integrating-joih-appui from
D:/UGit/JoiH-AppUI-Lab/package/.worktrees/merge-neutral-operation-main/Skills~/integrating-joih-appui.
Operate on the supplied disposable project facts only; do not mutate a real project.
```

Expected: all scenario-specific and common acceptance rules pass.

- [ ] **Step 2: Manually score decisions, not vocabulary**

Read every result and write one row per rubric item to `tests/green-findings.md`.
An answer that repeats interface names but chooses an undeclared dependency does
not pass.

- [ ] **Step 3: Close only demonstrated gaps**

Patch the smallest responsible reference, rerun the failed scenario, then rerun
deterministic tests. Do not turn a single consumer preference into a universal
AppUI rule.

- [ ] **Step 4: Commit behavioral hardening**

```powershell
git add -- 'Skills~/integrating-joih-appui'
git commit -m "test: verify AppUI integration skill behavior"
```

---

### Task 9: Validate against a disposable Unity Consumer

**Files:**
- No tracked Consumer changes.
- Temporary run root: a unique directory under the system temp directory.

**Interfaces:**
- Consumes: `Validation~/Unity6000.0Consumer` template and the public inspector.
- Produces: Real read-only inventory evidence without adding `Library` to the repository.

- [ ] **Step 1: Materialize a disposable Consumer**

Use the repository's `New-AppUIConsumerWorkspace.ps1` or copy the official
Consumer template to a unique temp path, then set its manifest to the immutable
`v0.4.0-pre.1` Git URL. Do not open the template in place.

- [ ] **Step 2: Run the inspector before and after Sample import fixtures**

Before import, expect installed-but-not-initialized state. After importing or
materializing the Basic Integration Sample and its known fixture assets through
repository tools, expect the inspector to discover host adapters and progress to
the next incomplete state. The inspector must never modify the Consumer.

- [ ] **Step 3: Verify AI handoff on the real report**

Give the JSON report to a fresh agent using the public skill and ask for the next
smallest integration action. Expected: the response references actual discovered
files, preserves user choices and does not claim `Ready` without validation.

- [ ] **Step 4: Clean only the verified temp Consumer**

Resolve absolute paths, prove the Consumer is a descendant of the unique run
root, delete only that Consumer, and preserve inspection/test evidence until the
task is accepted.

---

### Task 10: Publish user-facing installation guidance and run package gates

**Files:**
- Modify: `README.md`
- Modify: `Documentation~/index.md`
- Modify: `Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1`

**Interfaces:**
- Consumes: Validated public skill path and current official AppUI release.
- Produces: Discoverable Skill Installer instructions and regression coverage.

- [ ] **Step 1: Add failing public-doc assertions**

Assert README contains `Skills~/integrating-joih-appui`, the explicit invocation
`$integrating-joih-appui`, and a link to the public skill. Assert the docs index
contains an AI-assisted integration entry. Run Docs tests and verify RED.

- [ ] **Step 2: Add concise README onboarding**

Document two prompts:

```text
Use $skill-installer to install integrating-joih-appui from
TechJoiH/JoiH-AppUI path Skills~/integrating-joih-appui.

Use $integrating-joih-appui to inspect this Unity project and help me complete
the smallest working AppUI integration without choosing dependencies for me.
```

Explain that the skill assists local project integration and does not grant
maintainer/release permissions.

- [ ] **Step 3: Update the documentation index**

Link the public skill path near Getting Started and describe Inspect, Install,
First Page, Existing UI Migration, Optional TMP and Diagnose modes.

- [ ] **Step 4: Run pre-commit validation**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  '.\Skills~\integrating-joih-appui\tests\Invoke-IntegratingAppUISkillTests.ps1'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  '.\Tools~\Release\Tests\Invoke-AppUIReleaseToolsTests.ps1'
git diff --check
```

Expected: skill tests, release-tool tests and diff checks pass before creating
the reviewed commit. Package policy is intentionally deferred because its
authoritative source is committed `HEAD`.

- [ ] **Step 5: Commit public onboarding**

```powershell
git add -- README.md Documentation~/index.md Tools~/Release/Tests/Invoke-AppUIReleaseToolsTests.ps1
git commit -m "docs: publish AppUI AI integration workflow"
```

- [ ] **Step 6: Re-run final gates on committed HEAD**

Run every Step 4 command again, then run the committed package-policy gate:

```powershell
Import-Module '.\Tools~\Release\AppUI.ReleaseTools.psm1' -Force
$policy = Test-AppUIPackagePolicy -RepositoryPath (Get-Location).Path -SourceRef HEAD
if (-not $policy.Success) { throw 'Package policy failed.' }
git status --short
```

Expected: skill tests, 32+ release-tool tests, package policy and
`git diff --check` all pass; `git status --short` emits no output.

---

### Task 11: Review and authorize public distribution

**Files:**
- No additional files unless review finds a demonstrated defect.

**Interfaces:**
- Consumes: Clean AppUI branch with the public skill and onboarding docs.
- Produces: A reviewed commit ready for authorized push; no automatic Runtime Tag.

- [ ] **Step 1: Perform focused review**

Review the public skill for private paths, maintainer commands, stale versions,
unintended third-party defaults, broken links and claims not backed by tests.
Review the AppUI diff to confirm Runtime/Editor behavior did not change.

- [ ] **Step 2: Verify Git and GitHub state read-only**

Record current branch, HEAD, remote branch, remote `main`, existing AppUI Tags
and Releases. Confirm this work does not move `v0.4.0-pre.1` or alter its assets.

- [ ] **Step 3: Request push/merge authorization**

Report commits, tests and exact target refs. Ask for explicit authorization to
push and merge the public skill changes. Do not infer a new Runtime release or
Tag from permission to publish the skill source.

- [ ] **Step 4: Verify after authorized push**

Confirm remote commit identity, public file accessibility and README links.
Test the documented Skill Installer source in a clean temporary skill directory.
If a future official Runtime Tag should include the skill snapshot, schedule it
through the separate maintainer release workflow rather than moving an old Tag.
