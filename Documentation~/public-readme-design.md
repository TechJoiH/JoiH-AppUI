# Joi.H AppUI public README design

## Purpose

Replace the repository landing page with a Chinese-first onboarding document
for Unity developers who have no knowledge of the framework's source project.
The README must explain what Joi.H AppUI is, why it exists, how to install it,
and how to complete a minimal working integration without reading internals.

## Audience and language

- Primary audience: Unity developers evaluating a reusable UGUI framework.
- Assumed knowledge: Unity scenes, GameObjects, components, prefabs, UPM, and
  basic C# `async`/`await`.
- Main language: Simplified Chinese.
- API types, menu paths, package identifiers, and code remain in English.
- Tone: technical, direct, and evidence-based rather than promotional.

## Required landing-page structure

1. Product identity and one-sentence summary.
2. "What it is": the responsibilities covered by the framework.
3. "Why it exists": the recurring UI problems it centralizes.
4. Suitable and unsuitable project profiles.
5. Core capabilities grouped by page lifecycle, layers/scopes, asset loading,
   binding, focus navigation, input policy, groups, and notices.
6. A compact architecture diagram showing host, runtime host, manager/services,
   provider, definitions/registries, controllers, and Unity UI objects.
7. Requirements and compatibility status.
8. Git URL installation plus the explicit UniTask prerequisite.
9. A ten-minute quick start covering runtime root, layer roots, runtime profile,
   page definition, registry, opening a page, and shutdown ownership.
10. A provider-adapter example using only real public APIs.
11. Binding and validation menu paths.
12. Sample and automated-validation evidence.
13. Pre-release limitations, compatibility policy, and license status.
14. Links to focused documents under `Documentation~`.

## Accuracy constraints

- All code examples must match the public API in `Runtime` and compile in the
  clean consumer project or a dedicated documentation test fixture.
- Do not mention Annals, its scenes, services, page IDs, or resource stack.
- Do not claim that AppUI creates an EventSystem, owns scene persistence, or
  owns the application's asset provider lifecycle.
- Explain that `AppUIRuntimeHost.Shutdown()` runs before a project-owned
  provider is disposed.
- Explain that the built-in provider uses Resources and custom stacks implement
  `IUIAssetProvider`; custom editor identifiers use `IUIEditorAssetIdResolver`.
- Keep business services outside the package and inject them through
  application-owned controller/context composition.

## Claims allowed by current evidence

- UPM package ID `com.joih.appui`, version `0.1.0-pre.1`.
- Unity 6000.0+, UGUI 2.0, and UniTask 2.5.5 requirements.
- No third-party inspector dependency.
- EditMode 101/101 and PlayMode 8/8 automated tests pass.
- Windows x64 Mono and IL2CPP Development Player builds pass.
- Runtime hot-loop input contract measured zero allocation for 100,000 calls.
- The package contains no source-project namespace or business-page coupling.

The README must describe these as validation evidence, not as guarantees for
every host project.

## Pre-release and license messaging

- State clearly that `0.1.0-pre.1` is a pre-release package.
- Explain that public and serialized APIs may change before 1.0.
- Do not call the repository open source until a license is selected and added.
- Do not include a license badge or implied permission to redistribute.

## Verification before publication

1. Check every referenced type and menu path against the current source.
2. Verify installation snippets use the repository root as the UPM package.
3. Compile executable code examples in the clean Unity test project.
4. Run Markdown link and placeholder scans.
5. Re-run EditMode and PlayMode suites if any source or sample code changes.
6. Confirm Git diff contains only the README, this maintainer design document,
   and any explicitly required documentation test fixture.
