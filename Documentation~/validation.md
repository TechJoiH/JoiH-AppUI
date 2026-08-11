# Validation and release gates

## Required automated gates

1. Runtime and Editor assemblies compile in a clean Unity 6 project.
2. `rg` boundary scan finds no host-project namespaces, third-party inspector references, or
   project resource types.
3. EditMode contracts pass for focus, input, asset leases, registries, binding
   results, and notice scopes.
4. PlayMode integration contracts pass for page lifecycle, Cancel, resource
   release, Notice fallback/provider paths, real EventSystem input raycasts, and
   interrupted-operation stale-result safety.
5. The Basic Integration sample imports and compiles.
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
  Provider loading/lease release plus fallback/scope cleanup are covered automatically.
- Run Binding Validation without any asset modifications.

## Compatibility policy

`0.x` versions may change public and serialized APIs. Beginning with `1.0`,
serialized enum values, definition fields, public service interfaces, and
generated binding format require migration notes and compatibility tests.
