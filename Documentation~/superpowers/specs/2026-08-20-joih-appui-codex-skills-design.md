# Joi.H AppUI Codex Skills Design

## 1. Purpose

Joi.H AppUI needs two separately distributed Codex skills for two audiences:

1. `maintaining-joih-appui` lets the project maintainer resume development,
   validation and release work on a new computer without relying on chat history.
2. `integrating-joih-appui` lets an external Unity developer ask AI to inspect a
   project, install AppUI, adapt the required host boundaries, create the first
   page and validate the integration.

The skills must not depend on each other. The public integration skill must not
contain maintainer credentials, release permissions or private operational
history.

## 2. Design Principles

- Keep `SKILL.md` as a concise route map. Load detailed references only for the
  current task.
- Inspect the repository or Unity project before asking questions or choosing
  implementations.
- Derive paths, versions, Unity installations and project architecture from the
  current machine. Do not store machine-specific absolute paths.
- Do not store GitHub tokens, passwords, OAuth secrets or private keys.
- Read-only discovery may run automatically. Local project edits require a
  user request that clearly includes integration or implementation. Push, Tag
  and GitHub Release remain separate external mutations.
- Reuse repository-owned tools and public Samples before generating parallel
  implementations.
- A published immutable Tag is never moved, deleted or reused. A failure after
  Tag creation is a `Failed Release Attempt` and requires a new version.
- AppUI Core remains implementation-neutral. Neither skill may silently choose
  UniTask, Resources, Addressables, TextMeshPro or another third-party package.
- Technical validation does not replace license, third-party notice, visual,
  interaction or final-project acceptance.

## 3. Distribution

### 3.1 Maintainer skill

`maintaining-joih-appui` is first installed under the maintainer's personal
Codex skills directory. After validation it is published to a dedicated private
GitHub skill repository. The repository name is selected when publishing; the
skill itself contains no credentials. A new computer installs it through the
normal Codex skill installer after GitHub authentication is configured locally.

### 3.2 Public integration skill

`integrating-joih-appui` is stored in the public AppUI repository at:

```text
Skills~/integrating-joih-appui/
```

The trailing `~` keeps the skill outside the Unity Asset Database while leaving
it installable from the Git repository. AppUI README and onboarding documents
will show the skill installation and example invocation. The skill targets
official immutable AppUI releases, not the mutable `main` branch.

## 4. Maintainer Skill

### 4.1 Structure

```text
maintaining-joih-appui/
├── SKILL.md
├── agents/
│   └── openai.yaml
├── scripts/
│   ├── inspect-maintainer-environment.ps1
│   └── inspect-release-state.ps1
└── references/
    ├── new-machine-bootstrap.md
    ├── repository-map.md
    ├── development-workflow.md
    ├── release-runbook.md
    └── failure-recovery.md
```

### 4.2 Modes

| Mode | Trigger | Result |
|---|---|---|
| Bootstrap | New computer, missing checkout or unknown toolchain | Environment report and exact setup actions |
| Resume | Existing checkout or interrupted task | Current branch, worktree, remote and release-state reconciliation |
| Develop | Update, refactor, document or test AppUI | Repository-aware implementation and verification workflow |
| Validate | Prepare a candidate without publishing | Exact Commit/Tree/Version evidence and external Consumer gates |
| Release | Publish a verified version | Push, immutable Tag, Tag smoke, artifacts and GitHub Release |
| Recover | A release step failed or state is ambiguous | Read-only reconciliation and safe next-version decision |

### 4.3 Environment inspection

`inspect-maintainer-environment.ps1` is read-only and emits stable JSON. It
detects:

- operating system and PowerShell edition/version;
- Git and GitHub CLI availability and authenticated account state without
  printing credentials;
- Unity installations and the repository-declared official Editor target;
- Visual Studio C++ Build Tools and IL2CPP prerequisites;
- repository root, current branch, linked-worktree state and remotes;
- presence of `package.json`, `Tools~/Release`, `Validation~`, release tests and
  required public documents.

The script reports facts and missing capabilities. It does not install software,
sign in, clone, pull, edit, push or publish.

### 4.4 Release-state inspection

`inspect-release-state.ps1` reads the current package version, HEAD Commit/Tree,
dirty state, remote `main`, matching Tag and GitHub Release. It returns one of:

```text
EnvironmentBlocked
WorkingTreeDirty
CandidateUnverified
CandidateNotPushed
ReadyForTag
TagExistsUnverified
FailedReleaseAttempt
Published
RemoteStateAmbiguous
```

An ambiguous remote or GitHub response never authorizes a retry. The workflow
performs another read-only query before deciding whether a mutation occurred.

### 4.5 Release runbook

The runbook preserves the proven order:

1. Work in a clean worktree and preserve unrelated user changes.
2. Resolve an exact 40-character candidate Commit and Tree.
3. Run repository release-tool tests and static package policy.
4. Create deterministic package snapshots outside the repository.
5. Materialize clean external Consumer projects with no shared `Library`.
6. Run the repository-defined Binding, EditMode, PlayMode, Mono and IL2CPP
   gates for every supported integration profile.
7. Recheck candidate identity after Unity finishes.
8. Push the candidate only after the required authorization.
9. Install the exact Commit URL in another clean Consumer.
10. Confirm remote `main` equals the candidate and the planned Tag is unused.
11. Create and push one annotated immutable Tag.
12. Install the Tag URL in another clean Consumer.
13. Generate a formal report binding Repository, Commit, Tree, version,
    package-manifest hash, Tag and all gate results.
14. Sanitize artifacts, reject secrets and local absolute paths, and verify the
    exact artifact manifest and SHA-256 hashes.
15. Create the GitHub Release once. If the command result is uncertain, inspect
    it instead of blindly retrying.
16. Verify release flags, Tag identity, asset count, upload state and digests.
17. Commit and push post-release README, CHANGELOG, support-table and validation
    evidence updates without moving the Tag.
18. Delete only verified temporary Consumer directories and preserve evidence.

Repository scripts remain authoritative. The skill explains their contracts and
invokes them; it does not duplicate AppUI release implementation inside the
personal skill.

## 5. Public Integration Skill

### 5.1 Structure

```text
Skills~/integrating-joih-appui/
├── SKILL.md
├── agents/
│   └── openai.yaml
├── scripts/
│   └── inspect-appui-project.ps1
└── references/
    ├── installation.md
    ├── host-boundaries.md
    ├── runtime-root.md
    ├── page-production.md
    ├── binding-focus-input.md
    ├── optional-textmeshpro.md
    ├── migration.md
    └── troubleshooting.md
```

### 5.2 Invocation modes

| Mode | Example request |
|---|---|
| Inspect | "检查这个 Unity 项目能否接入 AppUI" |
| Install | "把官方 AppUI 版本安装到当前项目" |
| Minimal integration | "接好 AppUI Runtime，但先不要做业务页面" |
| First page | "用 AppUI 创建并打开第一个设置页面" |
| Existing UI migration | "把这个现有 UGUI 面板迁移到 AppUI" |
| Optional TMP | "这个项目选择 TMP，接入可选集成" |
| Diagnose | "Binding/Focus/Input/生命周期为什么不工作" |
| Upgrade | "把当前 AppUI 接入升级到新的官方版本" |

### 5.3 Read-only project inspection

`inspect-appui-project.ps1` accepts a path inside a Unity project and walks up to
the root containing `Assets`, `Packages` and `ProjectSettings`. It emits JSON
with:

- Unity Editor version from `ProjectVersion.txt`;
- AppUI manifest and lock-file references, resolved version and install source;
- asmdefs, scripting defines and installed UGUI/TMP/async/asset packages;
- likely composition roots, scene/bootstrap scripts and existing UI managers;
- existing AppUI adapters, runtime host, profiles, definitions, registries,
  Binding settings and generated bindings;
- imported Basic, Custom Host and TextMeshPro Samples;
- available tests and last discoverable validation outputs.

The script does not modify `manifest.json`, assets, defines or scenes. Secret
files and package caches outside the resolved AppUI package are not scanned.

### 5.4 Integration states

The skill maps facts to the next meaningful state:

```text
NotAUnityProject
UnityVersionUnverified
AppUINotInstalled
InstalledNotInitialized
HostBoundariesMissing
RuntimeRootIncomplete
PageContractIncomplete
BindingGenerationPending
BindingInvalid
RuntimeValidationPending
Ready
```

The AI reports the state, evidence and next action before editing. It asks only
for decisions that cannot be inferred safely, such as the project's chosen
operation, asset and execution implementations.

### 5.5 Source-of-truth order

For installation and migration, use this order:

1. immutable GitHub Tag and Release identity;
2. `supported-unity-versions.md` official release table;
3. package `package.json` and migration guide at that Tag;
4. installed package source and Samples;
5. README and tutorials.

If these disagree, stop and report the mismatch. Never install a Tag merely
because an older tutorial contains it. Never recommend `main` as a production
dependency.

### 5.6 Host-boundary workflow

AppUI requires three project-owned ports:

- `IUIOperationFactory`;
- `IUIAssetProvider` with explicit Lease ownership;
- `IAppUIExecutionContext` for Unity-main-thread commits.

The AI first detects existing project technologies, then presents compatible
choices. It may adapt the project's existing Task/Awaitable/callback/coroutine,
Addressables/AssetBundle/custom asset system and main-thread dispatcher. It must
not install or select a third-party package merely to shorten the example.

The Basic Integration Sample is the zero-third-party learning path. The Custom
Host Integration Sample is the reference for scene lifecycle, world-input
gating, pooling, Lease ownership and contract tests. Imported Sample code is a
starting point owned by the consumer, not a Runtime default.

### 5.7 UI production workflow

The public skill adopts the successful structure of the local
`annals-unity-ui-workflow` without its project-specific names:

1. Inspect the host composition root and current UI architecture.
2. Define the page contract: PageId, AssetId, Layer, CanvasDomain, Scope,
   OpenPolicy, Cancel, input blocking and focus behavior.
3. Create or adapt the Controller before binding-heavy Prefab work.
4. Inspect the authored Prefab contract and add `B_` only to runtime-needed
   nodes. Do not convert decorative children into runtime controls.
5. Generate bindings, wait for compilation/domain reload, bind references, then
   validate. Never collapse the two stages.
6. Create or synchronize Definition, Registry, Runtime Profile and Layer roots.
7. Initialize `AppUIRuntimeHost` with explicit project dependencies and optional
   immutable configuration.
8. Verify Open, Refresh, Close, Cancel, scene release, re-open and Shutdown.
9. Add default focus, direction navigation and world-input blocking only when
   required by the page contract.
10. Run Binding, Input and Focus validators, relevant EditMode/PlayMode tests and
    a Player Build proportional to the change.

When direct editing of Unity-managed `.unity`, `.prefab`, `.asset`, `.asmdef` or
`.meta` files is not authorized or safe, the skill creates project-owned C# or
Editor automation where appropriate and gives the user exact Inspector steps
for the remaining authored assets. It never claims completion before those
steps are verified.

### 5.8 Optional TextMeshPro

Base integration remains UGUI-only. When the project explicitly chooses TMP,
the skill follows the installed release documentation to:

1. add `JOIH_APPUI_TMP` for the intended build target;
2. wait for the optional Runtime/Editor assemblies;
3. import the TextMeshPro Integration Sample when useful;
4. enable `joih.appui.tmp` in Binding settings;
5. inject the TMP InputField resolver and explicit Dropdown policies;
6. configure explicit TMP Notice Views;
7. rerun Generate, Bind, Validate, diagnostics, tests and builds.

Installed TMP does not automatically enable the AppUI integration.

## 6. Safety And Authorization

| Action | Default |
|---|---|
| Inspect files, Git state, Unity version and installed packages | Allowed read-only |
| Create a local integration report | Allowed when requested as part of analysis |
| Edit the current Unity project for an explicit integration request | In scope; preserve unrelated changes |
| Install new third-party packages | Requires an explicit user choice |
| Modify Unity scenes, prefabs or serialized assets directly | Follow project rules or obtain explicit authorization |
| Push branches or commits | Requires explicit authorization unless already granted for the workflow |
| Create or move Tags | Create only with explicit authorization; never move |
| Create GitHub Releases | Requires explicit authorization; never blind-retry |
| Read or print credentials | Forbidden |

## 7. Testing Strategy

Each skill is created and deployed separately. Complete RED/GREEN/REFACTOR for
the maintainer skill before creating the public skill.

### 7.1 Maintainer scenarios

1. New computer with Git but no verified Unity/VS toolchain; the user asks to
   publish immediately. The skill must diagnose and stop before mutation.
2. A Tag exists but Tag smoke or artifact audit failed. The skill must preserve
   the Tag, classify the attempt and select a new version.
3. The primary worktree contains user changes while a clean release worktree is
   available. The skill must preserve the dirty worktree and release only the
   verified candidate.

### 7.2 Public integration scenarios

1. Clean Unity 6 project with no selected async, asset or text solution. The
   skill must not invent dependencies and must offer the zero-third-party path.
2. Existing project already uses Addressables and TMP. The skill must adapt the
   existing boundaries and enable TMP only after the base runtime closes its
   minimum lifecycle loop.
3. Existing UGUI page has business selection, focus, hover and world input. The
   skill must keep those states separate, use the two-stage Binding gate and
   validate Open/Refresh/Close/Cancel/scene release.
4. Installed AppUI version and an old tutorial disagree. The skill must trust
   immutable release evidence and stop before installing the stale Tag.

For every scenario, run an independent baseline without the skill, record the
observed omissions or unsafe assumptions, then rerun with the skill. Also run
the standard skill validator, script tests in isolated temporary directories,
frontmatter checks, reference-link checks and a real read-only pass against the
AppUI repository or a disposable Consumer project.

## 8. Documentation And Versioning

- Public README links the public skill installation command and example prompt.
- Public skill references the installed release documentation rather than
  copying versioned API details unnecessarily.
- A change to AppUI public contracts, required host ports, Binding stages,
  optional integration activation or official install policy requires review of
  the public skill in the same release.
- A change to release tooling or evidence contracts requires review of the
  maintainer skill.
- Official public-skill snapshots ship with AppUI repository Tags because the
  skill lives in the same repository. Skill source on `main` may be newer than
  the latest Runtime Release, so its compatibility statement must identify the
  AppUI versions it understands.

## 9. Implementation Order

1. Create, baseline-test, implement and validate `maintaining-joih-appui`.
2. Install it locally and verify a read-only resume/release audit against the
   current AppUI repository.
3. Create, baseline-test, implement and validate `integrating-joih-appui`.
4. Verify the public skill against a disposable Unity Consumer and the current
   public AppUI release.
5. Add public installation documentation to AppUI README.
6. Publish the maintainer skill to a private repository and the public skill to
   the AppUI repository only after their separate review and authorization.
