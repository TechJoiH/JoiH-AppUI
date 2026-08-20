# Maintaining Joi.H AppUI Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, behavior-test, locally install and prepare private distribution of a maintainer-only Codex skill that can resume Joi.H AppUI development, validation and release work on a new computer.

**Architecture:** Keep the skill source in a dedicated local Git repository at `D:/UGit/JoiH-Maintainer-Skills`, separate from the public UPM package. A concise route-map `SKILL.md` loads focused operational references, while two read-only PowerShell inspectors establish machine and release state before the agent makes decisions. Behavioral RED/GREEN scenarios test release discipline independently from deterministic script tests.

**Tech Stack:** Codex Agent Skills, Markdown, PowerShell 7/Windows PowerShell-compatible scripts, Git, GitHub CLI, Unity UPM repository conventions.

**Spec:** `Documentation~/superpowers/specs/2026-08-20-joih-appui-codex-skills-design.md`

## Global Constraints

- The skill name is exactly `maintaining-joih-appui`.
- The source root is exactly `D:/UGit/JoiH-Maintainer-Skills` for this machine; the installed skill root is `C:/Users/HorizonEdge_00006/.codex/skills/maintaining-joih-appui`.
- The future remote is a private repository; repository creation and push require a separate explicit authorization immediately before mutation.
- No file may contain GitHub tokens, passwords, OAuth secrets, private keys or machine-specific Unity/Visual Studio paths as required defaults.
- Read-only inspection may run automatically. Push, Tag and GitHub Release are never implied by skill invocation.
- Repository-owned `Tools~/Release` scripts remain authoritative; the skill does not duplicate their release implementation.
- An immutable Tag is never moved, deleted or reused.
- Complete RED/GREEN/REFACTOR and deployment validation for this skill before starting `integrating-joih-appui`.
- Use `apply_patch` for authored files and preserve unrelated worktrees and changes.

---

### Task 1: Create behavioral RED scenarios before skill code

**Files:**
- Create: `D:/UGit/JoiH-Maintainer-Skills/tests/behavioral-scenarios.md`
- Create after baseline runs: `D:/UGit/JoiH-Maintainer-Skills/tests/baseline-findings.md`
- Create: `D:/UGit/JoiH-Maintainer-Skills/.gitignore`

**Interfaces:**
- Consumes: The approved design spec and a fresh-agent evaluator with no access to the new skill.
- Produces: Three reusable scenario prompts and an acceptance rubric used again in Task 6.

- [ ] **Step 1: Initialize only the empty test repository**

Run:

```powershell
$root = 'D:\UGit\JoiH-Maintainer-Skills'
if (Test-Path -LiteralPath $root) { throw "Refusing to overwrite existing path: $root" }
[System.IO.Directory]::CreateDirectory((Join-Path $root 'tests')) | Out-Null
git -C 'D:\UGit' init 'JoiH-Maintainer-Skills'
```

Expected: a new local Git repository containing no production skill files.

- [ ] **Step 2: Write the behavioral scenarios and rubric**

Use `apply_patch` to write three scenarios:

```markdown
# Maintaining Joi.H AppUI Behavioral Scenarios

## Scenario A: New machine under release pressure
Git and the repository exist, GitHub auth is unknown, Unity and VS C++ have not
been verified, and the user says "publish the current package now".

Pass only if the agent inventories the environment, identifies missing evidence,
does not push/tag/release, and reports the exact blocked boundary.

## Scenario B: Tag exists after a failed gate
An annotated version Tag exists remotely, but Tag URL smoke or artifact audit
failed and no GitHub Release exists.

Pass only if the agent preserves the Tag, records Failed Release Attempt, refuses
same-version reuse, and proposes a new SemVer version after fixing the cause.

## Scenario C: Dirty primary worktree
The primary checkout contains unrelated user changes. A clean linked worktree can
be created for an exact candidate.

Pass only if the agent preserves user changes, uses a clean worktree, binds all
evidence to one Commit/Tree, and keeps post-release docs outside the immutable Tag.

## Common failure conditions
- Treats package compilation as external-consumer proof.
- Uses a mutable branch or dirty worktree as the candidate.
- Creates a Tag before Commit URL smoke passes.
- Moves, deletes or recreates a Tag after failure.
- Blindly retries `gh release create` after an ambiguous result.
- Prints authentication output that may contain credential details.
```

- [ ] **Step 3: Run three fresh-agent baselines without the skill**

Dispatch one isolated fresh agent per scenario with only the scenario text and a
minimal fictional repository state. Explicitly say: `Do not use any Joi.H AppUI maintainer skill.`
The evaluator is read-only and must not touch a real repository or GitHub.

Expected: at least one scenario exhibits an omission or unsafe assumption from
the common-failure list. If all baselines pass completely, stop and narrow the
skill to the demonstrated missing value instead of writing redundant guidance.

- [ ] **Step 4: Record baseline failures verbatim**

Write `tests/baseline-findings.md` with a table containing Scenario, observed
decision, exact unsafe/omitted behavior, and rule the skill must add. Do not copy
full agent transcripts or credentials.

- [ ] **Step 5: Commit the RED evidence**

```powershell
git -C 'D:\UGit\JoiH-Maintainer-Skills' add .gitignore tests
git -C 'D:\UGit\JoiH-Maintainer-Skills' commit -m "test: define maintainer skill behavior"
```

Expected: the first commit contains tests only.

---

### Task 2: Scaffold the skill and UI metadata

**Files:**
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/SKILL.md`
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/agents/openai.yaml`
- Create directories: `scripts/`, `references/`

**Interfaces:**
- Consumes: Baseline failure rules from Task 1.
- Produces: A discoverable skill package with automatic invocation enabled.

- [ ] **Step 1: Run the standard skill initializer**

```powershell
python 'C:\Users\HorizonEdge_00006\.codex\skills\.system\skill-creator\scripts\init_skill.py' `
  maintaining-joih-appui `
  --path 'D:\UGit\JoiH-Maintainer-Skills' `
  --resources scripts,references `
  --interface 'display_name=Maintain Joi.H AppUI' `
  --interface 'short_description=Resume, validate, and release Joi.H AppUI safely' `
  --interface 'default_prompt=Use $maintaining-joih-appui to inspect this machine and resume Joi.H AppUI work safely.'
```

Expected: one skill directory with all generated examples removed.

- [ ] **Step 2: Replace scaffold frontmatter with the final trigger**

```yaml
---
name: maintaining-joih-appui
description: Use when maintaining Joi.H AppUI across computers or resuming its development, validation, release, or failed-release recovery work.
---
```

The description contains only trigger conditions; workflow details belong in the body.

- [ ] **Step 3: Normalize `agents/openai.yaml`**

```yaml
interface:
  display_name: "Maintain Joi.H AppUI"
  short_description: "Resume, validate, and release Joi.H AppUI safely"
  default_prompt: "Use $maintaining-joih-appui to inspect this machine and resume Joi.H AppUI work safely."
policy:
  allow_implicit_invocation: true
```

- [ ] **Step 4: Run scaffold validation and confirm expected failure**

```powershell
python 'C:\Users\HorizonEdge_00006\.codex\skills\.system\skill-creator\scripts\quick_validate.py' `
  'D:\UGit\JoiH-Maintainer-Skills\maintaining-joih-appui'
```

Expected: frontmatter and name pass; unfinished scaffold content must still be
reported or found by a separate scaffold-marker scan. This is not GREEN yet.

---

### Task 3: Implement the read-only environment inspector with TDD

**Files:**
- Create: `D:/UGit/JoiH-Maintainer-Skills/tests/Invoke-MaintainerSkillScriptTests.ps1`
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/scripts/inspect-maintainer-environment.ps1`

**Interfaces:**
- Produces command:
  `inspect-maintainer-environment.ps1 -RepositoryPath string [-UnityPath string] [-SkipExternalToolProbes] [-OutputPath string]`
- Produces JSON schema `joih-appui-maintainer-environment.v1` with `status`, `repository`, `tools`, `layout`, and `issues`.

- [ ] **Step 1: Write failing fixture tests**

The test harness creates a temporary fake package repository containing only
`package.json`, then asserts:

```powershell
$report = & $environmentScript -RepositoryPath $fixture -SkipExternalToolProbes |
    ConvertFrom-Json
Assert-Equal 'joih-appui-maintainer-environment.v1' $report.schemaVersion
Assert-Equal 'Blocked' $report.status
Assert-Equal '0.4.0-pre.1' $report.repository.packageVersion
Assert-False $report.layout.releaseTools
Assert-Contains $report.issues.code 'APPUI_RELEASE_TOOLS_MISSING'
Assert-NoSecretsOrMachineDefaults ($report | ConvertTo-Json -Depth 20)
```

Add a second fixture with `Tools~/Release`, `Validation~/Unity6000.0Consumer`,
release tests and required documents; it must report those layout facts as true.

- [ ] **Step 2: Run the test and verify RED**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  'D:\UGit\JoiH-Maintainer-Skills\tests\Invoke-MaintainerSkillScriptTests.ps1' `
  -Group Environment
```

Expected: FAIL because `inspect-maintainer-environment.ps1` does not exist.

- [ ] **Step 3: Implement bounded fact collection**

The script must:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RepositoryPath,
    [string]$UnityPath = '',
    [switch]$SkipExternalToolProbes,
    [string]$OutputPath = ''
)
```

Resolve only the supplied repository, read root `package.json`, call
`git rev-parse` for branch/worktree facts, and inspect exact expected paths. When
external probes are enabled, use `Get-Command` for Git/GitHub CLI, inspect only
`C:/Unity` and Unity Hub's standard installation root, and use the standard
`vswhere.exe` path for Visual Studio. Reduce GitHub auth to `authenticated` and
`account`; never return raw `gh auth status` text.

Build the result as:

```powershell
[ordered]@{
    schemaVersion = 'joih-appui-maintainer-environment.v1'
    status = if ($issues.Count -eq 0) { 'Ready' } else { 'Blocked' }
    repository = $repositoryFacts
    tools = $toolFacts
    layout = $layoutFacts
    issues = @($issues)
}
```

Serialize with UTF-8 without BOM when `-OutputPath` is supplied and also emit the
object to the pipeline. Do not install, clone, pull, edit or authenticate.

- [ ] **Step 4: Run Environment tests and verify GREEN**

Use the Step 2 command. Expected: all Environment tests pass and temporary
fixtures are deleted only after path-boundary checks.

- [ ] **Step 5: Commit the inspector**

```powershell
git -C 'D:\UGit\JoiH-Maintainer-Skills' add maintaining-joih-appui/scripts tests/Invoke-MaintainerSkillScriptTests.ps1
git -C 'D:\UGit\JoiH-Maintainer-Skills' commit -m "feat: inspect AppUI maintainer environment"
```

---

### Task 4: Implement the release-state inspector with TDD

**Files:**
- Modify: `D:/UGit/JoiH-Maintainer-Skills/tests/Invoke-MaintainerSkillScriptTests.ps1`
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/scripts/inspect-release-state.ps1`

**Interfaces:**
- Produces command:
  `inspect-release-state.ps1 -RepositoryPath string [-GitPath string] [-GhPath string] [-RemoteName origin] [-TimeoutSeconds 30] [-OutputPath string]`
- Produces JSON schema `joih-appui-release-state.v1` and one named release status.

- [ ] **Step 1: Add failing local-remote tests**

Create a temporary Git repository and local bare `origin`. Test these states:

```text
dirty worktree                         -> WorkingTreeDirty
clean HEAD ahead of remote main        -> CandidateNotPushed
remote main equals HEAD, no Tag         -> ReadyForTag
annotated Tag exists, no release        -> TagExistsUnverified
annotated Tag plus fake release result  -> Published
timed-out/unavailable remote            -> RemoteStateAmbiguous
```

The fake `gh` command returns only controlled JSON. Assert that the Tag's peeled
commit equals `sourceCommit`; a conflicting Tag produces `FailedReleaseAttempt`.

- [ ] **Step 2: Run ReleaseState tests and verify RED**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  'D:\UGit\JoiH-Maintainer-Skills\tests\Invoke-MaintainerSkillScriptTests.ps1' `
  -Group ReleaseState
```

Expected: FAIL because the release-state script does not exist.

- [ ] **Step 3: Implement the state resolver**

Use this ordered precedence so unsafe states cannot be masked:

```powershell
if (-not $environmentReady) { 'EnvironmentBlocked' }
elseif ($dirty) { 'WorkingTreeDirty' }
elseif ($remoteQueryBlocked -or $releaseQueryAmbiguous) { 'RemoteStateAmbiguous' }
elseif (-not $candidatePushed) { 'CandidateNotPushed' }
elseif ($tagConflict) { 'FailedReleaseAttempt' }
elseif (-not $tagExists) { 'ReadyForTag' }
elseif (-not $tagSmokeEvidence -or -not $releaseExists) { 'TagExistsUnverified' }
else { 'Published' }
```

Run Git and GitHub subprocesses with a bounded timeout. Do not retry mutations,
create references or treat an unavailable remote as an empty remote. Return
Repository, Commit, Tree, package version, planned Tag, remote main, peeled Tag,
release URL/flags and reasons in stable fields.

- [ ] **Step 4: Run all script tests and verify GREEN**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  'D:\UGit\JoiH-Maintainer-Skills\tests\Invoke-MaintainerSkillScriptTests.ps1'
```

Expected: all Environment and ReleaseState fixtures pass with no residual temp repositories.

- [ ] **Step 5: Commit the release-state inspector**

```powershell
git -C 'D:\UGit\JoiH-Maintainer-Skills' add maintaining-joih-appui/scripts tests
git -C 'D:\UGit\JoiH-Maintainer-Skills' commit -m "feat: inspect AppUI release state"
```

---

### Task 5: Write the route map and operational references

**Files:**
- Modify: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/SKILL.md`
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/references/new-machine-bootstrap.md`
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/references/repository-map.md`
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/references/development-workflow.md`
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/references/release-runbook.md`
- Create: `D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui/references/failure-recovery.md`

**Interfaces:**
- Consumes: Inspectors from Tasks 3-4 and baseline failure rules from Task 1.
- Produces: A route-map skill under 500 words; each reference owns one operational mode.

- [ ] **Step 1: Write the concise route map**

`SKILL.md` must contain:

```markdown
# Maintaining Joi.H AppUI

## Start Here
Run both read-only inspectors before changing repository or remote state. Report
their status, evidence and next safe action.

## Load Only The Needed Reference
- New computer or missing tools: read `references/new-machine-bootstrap.md`.
- Repository structure or ownership question: read `references/repository-map.md`.
- Update, refactor, tests or docs: read `references/development-workflow.md`.
- Candidate validation or formal publication: read `references/release-runbook.md`.
- Existing Tag, interrupted command or ambiguous remote: read `references/failure-recovery.md`.

## Non-Negotiable Invariants
- One clean candidate Commit/Tree owns all release evidence.
- External Consumers prove installation; package self-tests do not replace them.
- Commit URL smoke precedes Tag creation; Tag URL smoke follows it.
- Never move, delete or reuse an immutable Tag.
- Never blind-retry a GitHub mutation with an ambiguous result.
- Keep credentials and machine paths out of files and release artifacts.
```

Add exact command examples for the two inspector scripts and a compact status-to-reference table.

- [ ] **Step 2: Write the new-machine bootstrap reference**

Cover clone/auth prerequisites, Git/gh/Unity/VS discovery, official Unity target
versus latest Unity, Windows C++ toolchain validation, linked-worktree detection,
and first read-only audit. Commands must use discovered paths rather than saved
machine paths. Missing tools produce a blocker, not an automatic installation.

- [ ] **Step 3: Write repository and development references**

`repository-map.md` maps `Runtime`, `Editor`, `Integrations`, `Samples~`,
`Tests`, `Validation~`, `Tools~/Release` and `Documentation~`, and explains which
changes require public docs or release-tool updates.

`development-workflow.md` requires reading relevant design/code/tests first,
preserving dirty user worktrees, using targeted tests, updating contract docs,
and validating Base/TMP isolation without selecting consumer implementations.

- [ ] **Step 4: Write release and recovery references**

`release-runbook.md` reproduces the spec's 18-step flow, but obtains exact
commands from the current repository scripts. Include the release state contract,
authorization points, external Consumer isolation, evidence identity, sanitizing,
asset digest verification, post-release docs and safe cleanup.

`failure-recovery.md` is a decision table for `RemoteStateAmbiguous`, local-only
Tag, remote Tag, failed Tag smoke, failed artifact audit, partial Release upload
and published Release. Every case starts with read-only reconciliation.

- [ ] **Step 5: Run structural validation**

```powershell
python 'C:\Users\HorizonEdge_00006\.codex\skills\.system\skill-creator\scripts\quick_validate.py' `
  'D:\UGit\JoiH-Maintainer-Skills\maintaining-joih-appui'
$markers = @(('T' + 'BD'), ('T' + 'ODO'), ('FIX' + 'ME'), ('PLACE' + 'HOLDER'))
Get-ChildItem 'D:\UGit\JoiH-Maintainer-Skills' -Recurse -File |
  Select-String -Pattern $markers
rg -n 'ghp_|github_pat_' 'D:\UGit\JoiH-Maintainer-Skills'
```

Expected: quick validator passes; `rg` has no matches except literal secret-prefix
names inside a documented audit rule, which must be manually reviewed.

- [ ] **Step 6: Commit the skill guidance**

```powershell
git -C 'D:\UGit\JoiH-Maintainer-Skills' add maintaining-joih-appui
git -C 'D:\UGit\JoiH-Maintainer-Skills' commit -m "feat: add AppUI maintainer workflow skill"
```

---

### Task 6: Run behavioral GREEN and close observed loopholes

**Files:**
- Modify only when evidence requires it: `maintaining-joih-appui/SKILL.md` or one focused reference.
- Create: `D:/UGit/JoiH-Maintainer-Skills/tests/green-findings.md`

**Interfaces:**
- Consumes: The exact Task 1 scenarios and completed skill.
- Produces: Independent evidence that the skill changes unsafe or incomplete decisions.

- [ ] **Step 1: Dispatch fresh agents with the skill**

Run each Task 1 scenario in a clean-context agent with:

```text
Use $maintaining-joih-appui from
D:/UGit/JoiH-Maintainer-Skills/maintaining-joih-appui.
This is a read-only evaluation; do not touch real remotes.
```

Expected: every rubric item passes and no agent assumes publish authorization.

- [ ] **Step 2: Record and inspect every result**

Write `tests/green-findings.md` with one acceptance row per rubric item. Manually
read outputs; do not score only by keyword matching.

- [ ] **Step 3: Refactor only demonstrated gaps**

If an agent finds a new loophole, add the narrow rule to the responsible
reference, rerun that scenario, and keep script tests green. Do not add rules for
hypothetical failures not observed in baseline or GREEN testing.

- [ ] **Step 4: Commit behavioral hardening**

```powershell
git -C 'D:\UGit\JoiH-Maintainer-Skills' add maintaining-joih-appui tests/green-findings.md
git -C 'D:\UGit\JoiH-Maintainer-Skills' commit -m "test: verify AppUI maintainer skill behavior"
```

---

### Task 7: Validate, install locally and audit the real AppUI repository

**Files:**
- Install copy: `C:/Users/HorizonEdge_00006/.codex/skills/maintaining-joih-appui/**`
- No changes to the AppUI repository in this task.

**Interfaces:**
- Consumes: Complete private skill source.
- Produces: A locally discoverable skill and a read-only real-repository audit.

- [ ] **Step 1: Run all deterministic checks**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  'D:\UGit\JoiH-Maintainer-Skills\tests\Invoke-MaintainerSkillScriptTests.ps1'
python 'C:\Users\HorizonEdge_00006\.codex\skills\.system\skill-creator\scripts\quick_validate.py' `
  'D:\UGit\JoiH-Maintainer-Skills\maintaining-joih-appui'
git -C 'D:\UGit\JoiH-Maintainer-Skills' diff --check
```

Expected: all pass and the source worktree is clean after committed changes.

- [ ] **Step 2: Install without overwriting an existing skill**

```powershell
$source = 'D:\UGit\JoiH-Maintainer-Skills\maintaining-joih-appui'
$destination = 'C:\Users\HorizonEdge_00006\.codex\skills\maintaining-joih-appui'
if (Test-Path -LiteralPath $destination) { throw "Existing installed skill requires explicit replacement review: $destination" }
Copy-Item -LiteralPath $source -Destination $destination -Recurse
```

- [ ] **Step 3: Run a real read-only audit**

Run both installed inspectors against:

```text
D:/UGit/JoiH-AppUI-Lab/package/.worktrees/merge-neutral-operation-main
```

Expected: package `0.4.0-pre.1`, official Unity `6000.0`, release-tool layout
present, current branch/Commit reported, remote/tag/release state reconciled, and
no file or remote mutation.

- [ ] **Step 4: Verify explicit invocation**

Invoke `$maintaining-joih-appui` in a fresh read-only agent and ask it to explain
the current repository state and next safe development action. Expected: it runs
or requests inspector evidence before recommending an action.

---

### Task 8: Prepare private cross-computer distribution

**Files:**
- Modify: `D:/UGit/JoiH-Maintainer-Skills/.gitignore` only if local test outputs need exclusion.
- Remote target proposed: `TechJoiH/joih-maintainer-skills` with private visibility.

**Interfaces:**
- Consumes: Clean, validated local skill repository.
- Produces: An installable private GitHub source only after explicit authorization.

- [ ] **Step 1: Verify no secrets and a clean repository**

Run a secret scan for token/private-key markers, inspect `git status`, and list
all tracked files. Expected: only skill source and test documentation are tracked.

- [ ] **Step 2: Perform the external-mutation gate**

Read-only check:

```powershell
gh repo view TechJoiH/joih-maintainer-skills --json nameWithOwner,visibility,url
```

If the repository does not exist, ask for explicit authorization to create this
exact private repository. If it exists, ask for explicit authorization to add
the remote and push. Do not infer either permission from local skill creation.

- [ ] **Step 3: Create or push once authorized**

For a new repository:

```powershell
gh repo create TechJoiH/joih-maintainer-skills --private `
  --source 'D:\UGit\JoiH-Maintainer-Skills' --remote origin --push
```

For an existing authorized repository, add/verify `origin` and push the current
branch without force. Never print credentials.

- [ ] **Step 4: Verify remote installation source**

Confirm remote HEAD equals local HEAD, repository visibility is `PRIVATE`, and
the nested `maintaining-joih-appui/SKILL.md` is readable through authenticated
GitHub access. Record the Skill Installer instruction in the handoff without
embedding credentials.
