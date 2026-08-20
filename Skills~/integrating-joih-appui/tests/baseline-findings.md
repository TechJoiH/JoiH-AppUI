# Integrating Joi.H AppUI Baseline Findings

These RED baselines were run in four fresh isolated contexts before any public
`SKILL.md` or production script existed. Each agent received only its
scenario-specific public AppUI documentation and Samples, was denied the future
skill and planning material, and performed read-only analysis against fictional
project state.

## Scenario A: Blank Unity 6 project

**Observed behavior:** Passed. The agent chose the supported immutable
`v0.4.0-pre.1` Tag, kept the page on UGUI, used the zero-third-party Basic
Integration path, accounted for the operation, asset, and execution ports, and
included Open, Refresh, Close, scene release, and Shutdown validation.

> “It is the smallest supported choice when you have not selected an async
> backend, asset loader, or text stack: it supplies a callback-based operation
> factory, main-thread context, and explicit prefab-reference asset
> provider—without adding TMP or third-party packages.”

**Violated rubric:** None.

**Smallest guidance needed:** None. Preserve this scenario as regression
coverage; do not add guidance that restates the baseline's correct behavior.

## Scenario B: Existing Addressables and TMP project

**Observed behavior:** The agent correctly adapted Addressables Lease ownership,
the existing main-thread dispatcher, the project's operation model, all three
host ports, and the explicit TMP provider and resolver. It did not first prove a
Base UGUI lifecycle loop; its first proposed runtime page was already TMP-backed.

> “首个 TMP 页面我会用一个最小、无池化、无动画的 Popup/Overlay 页面验证闭环。”

**Violated rubric:** Scenario B requires the Base UGUI runtime loop to close
before optional TMP activation.

**Smallest guidance needed:** Give one ordered route: adapt and inject the three
host ports, prove a Base UGUI Open/Refresh/Close/scene-release/Shutdown loop, and
only then enable `JOIH_APPUI_TMP`, `joih.appui.tmp`, and the TMP focus resolver.

## Scenario C: Existing interactive UGUI page

**Observed behavior:** The agent kept Selection, Focus, Hover, world-input
blocking, and Cancel separate and proposed broad lifecycle coverage. Its change
order was Definition, Controller, interaction policies, Binding, then host
runtime rather than Controller, Prefab, Binding, Definition, runtime, input. It
also collapsed Generate and Bind without an explicit compilation/domain-reload
boundary.

> “Convert prefab references to generated bindings: mark controller-accessed
> nodes with `B_`, generate first, then bind serialized references.”

**Violated rubric:** The required inspection order and the two-stage Binding
gate were both omitted.

**Smallest guidance needed:** Shape migration output as the fixed ordered recipe
Controller → Prefab → Binding → Definition → runtime → input, with Generate →
compilation/domain reload → Bind → Validate written as explicit Binding stages.

## Scenario D: Version sources disagree

**Observed behavior:** The agent correctly selected the newer immutable
`v0.4.0-pre.1` Release, refused the older tutorial Tag as the final target, and
required an installed-version migration baseline before editing. While
describing that baseline, it introduced an unsafe Binding sequence.

> “Reproduce the old version’s Generate → Bind → Validate workflow and a Player
> build.”

**Violated rubric:** The scenario-specific version rubric passed, but the common
failure condition prohibiting Generate and Bind without a
compilation/domain-reload boundary was triggered.

**Smallest guidance needed:** Make every Binding recipe structurally include the
compilation/domain-reload slot: Generate → compilation/domain reload → Bind →
Validate.

## Cross-scenario RED result

The RED gate is established: Scenario A passed as useful regression coverage,
while Scenarios B, C, and D exposed sequence or output-shape gaps. Future skill
guidance should address only those observed gaps and should be rerun against the
same four prompts during GREEN.
