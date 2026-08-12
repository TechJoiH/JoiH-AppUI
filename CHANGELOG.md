# Changelog

## [0.2.0-pre.3] - Unreleased

### Fixed

- Exported the remote Tag identity resolver required by the standalone Tag URL smoke entry point.
- Added a regression test that imports the release module and verifies the Tag smoke dependency is publicly callable.

### Release policy

- All Pre-tag, Commit SHA, Tag URL and release evidence must be regenerated for the new `0.2.0-pre.3` Commit and Tree.
- `v0.2.0-pre.2` remains immutable and is never moved, deleted or reused.

## [0.2.0-pre.2] - 2026-08-12 (Tag only; no GitHub Release)

### Failed Release Attempt

- Full Pre-tag validation, Mono/IL2CPP builds and Commit SHA Git URL smoke passed for Commit `2ba1c90f732b429b3b76cd2d8bcba73a4bb486cc`.
- The immutable Tag was created, but Tag URL smoke stopped before Unity because the release module did not export `Resolve-AppUIRemoteTagIdentity`.
- No GitHub Release was created, and this Tag is not an Officially Supported Release.

### Added

- Documented Unity 6.0 / `6000.0` as the single Official Target without tying the decision to Unity's upstream support calendar.
- Added five mutually exclusive compatibility states and an external-evidence-only Community Verified index.
- Added a standalone Community Unity Porting Guide for project-owned manifests, compatibility boundaries, validation and unofficial tags.
- Added a deterministic candidate snapshot with Commit, Tree and normalized package content identity.
- Added the clean `Validation~/Unity6000.0Consumer/` template with project-owned Operation, Execution Context and Asset Provider adapters.
- Added generated Basic Page, Popup, Binding and Focus fixtures plus external EditMode, PlayMode, Binding, Smoke, Mono and IL2CPP gates.
- Added bounded release orchestration, NUnit parsing, report identity checks, remote Tag verification, log redaction and secret auditing.
- Added an explicit Unity/Visual Studio 2022 C++/Windows SDK preflight with machine-readable `Blocked` evidence before full IL2CPP validation; explicit installation-path overrides are probed instead of trusted.
- Added strict SemVer, single package manifest, single official Consumer, remote Tag identity and exact ten-artifact release gates.
- Added bounded remote Git queries that distinguish timeout and unavailable remotes from valid release-readiness states.
- Hardened sanitized Unity log archives to redact all remaining local path roots and fail the Pre-tag gate if a secret or absolute machine path survives.
- `ReleaseScope` now invalidates matching in-flight and queued Open requests before an instance exists; late successful loads cannot reopen the page and release their Lease once.

## [0.2.0-pre.1]

### Breaking

- Public page APIs now return backend-neutral `IUIOperation<T>` and use `Open`、`Refresh`、`Close`、`Cancel`、`BindScene`、`UnbindScene`、`ReleaseScope` names.
- Runtime initialization now requires `AppUIRuntimeDependencies` with an `IUIOperationFactory`, `IUIAssetProvider`, and `IAppUIExecutionContext`.
- Removed the third-party async package dependency and all related assembly references.
- Removed the built-in Resources asset provider, automatic provider fallback, and automatic Awake initialization.
- Replaced controller async animation hooks with `UITransition` and project-owned Operations.

### Added

- Added `Joi.H.AppUI.Core` contract assembly.
- Added callback-driven page, scene, flow, focus virtualization, and transition state machines.
- Added an opt-in Basic Integration Sample with pure callback operations, Unity execution context, and explicit-reference asset loading.
- Added Chinese-first public documentation for integration, concepts, architecture, lifecycle, Binding, focus, input, validation, and FAQ.

## [0.1.0-pre.1]

- Established `com.joih.appui` and namespace `Joi.H.AppUI`.
- Added page/layer/scope lifecycle, Binding, focus navigation, input policies, notices, Editor validation, and initial contract tests.
- This version depended on a concrete async package and included a Resources default; both boundaries were intentionally removed in `0.2.0-pre.1` before stable release.
