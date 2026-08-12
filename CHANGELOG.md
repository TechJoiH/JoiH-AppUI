# Changelog

## [Unreleased]

### Added

- Documented Unity 6.0 / `6000.0` as the single Official Target without tying the decision to Unity's upstream support calendar.
- Added five mutually exclusive compatibility states and an external-evidence-only Community Verified index.
- Added a standalone Community Unity Porting Guide for project-owned manifests, compatibility boundaries, validation and unofficial tags.

### Planned validation work

- Formalize `Validation~/Unity6000.0Consumer/` as the only official external Consumer template.
- Add deterministic candidate snapshots, content manifests and external release reports.
- Complete IL2CPP, pushed Commit Git URL and immutable Tag Git URL gates before declaring an Officially Supported Release.

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
