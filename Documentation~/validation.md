# Validation and release gates

## Required automated gates

1. Runtime and Editor assemblies compile in a clean Unity 6 project.
2. Boundary scan finds no host-project namespaces, concrete async backend,
   Resources fallback, third-party inspector, or project resource types.
3. EditMode contracts pass for focus, input, asset leases, registries, binding
   results, and notice scopes.
4. PlayMode integration contracts pass for page lifecycle, Cancel, resource
   release, provider paths, real EventSystem input raycasts, and
   interrupted-operation stale-result safety.
5. The Basic Integration sample imports and compiles without third-party packages.
6. An IL2CPP development build completes for at least one supported target.

For Unity `6000.0.25f1`, use Visual Studio 2022 C++ Build Tools in repeatable
Windows CI. Visual Studio 2026 may require a process-local `VS170COMNTOOLS`
compatibility alias because this Unity revision classifies VS 18 as unknown.

## Manual gates

- Smoke-test open, cancel, refresh, hide, reopen, and release with final authored
  project prefabs. The functional lifecycle and Cancel paths are covered automatically.
- Interrupt an authored-prefab in-flight open and visually confirm stale work
  does not flash on screen. State and lease safety are covered automatically.
- Verify mouse and controller focus use the same committed selection state.
- Visually verify authored overlay, popup, and modal hit surfaces match their
  intended `PassChannelMask` regions. EventSystem raycast semantics are covered automatically.
- Visually verify final provider-backed Notice art, layout, and animation.
  Provider loading/lease release and scope cleanup are covered automatically.
- Run Binding Validation without any asset modifications.

## Compatibility policy

`0.x` versions may change public and serialized APIs. Beginning with `1.0`,
serialized enum values, definition fields, public service interfaces, and
generated binding format require migration notes and compatibility tests.

## 0.2.0-pre.1 current evidence

- Independent Unity 6 consumer with no third-party async package: Domain Reload passed.
- Final EditMode suite: 125/125 passed.
- Final PlayMode suite: 11/11 passed. This total includes the imported Callback
  Sample integration and real Open/Refresh/Close lifecycle tests.
- Windows x64 Mono Development Player Build: passed, 121,228,937 bytes.
- Windows x64 IL2CPP Build: blocked before C++ compilation because the current
  machine no longer has a discoverable Visual Studio C++ Build Tools install.
  This is an environment gate and must be rerun before public release.

These numbers describe the current local release candidate and must be refreshed
after any later code change before publishing.
