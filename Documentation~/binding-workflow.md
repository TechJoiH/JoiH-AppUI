# Binding workflow

AppUI binding uses two explicit stages so generated code and prefab mutation are
reviewable independently.

## Stage 1: generate

The scanner reads the controller and prefab hierarchy, then produces a generated
partial class in the configured output folder. Generation does not bind prefab
references.

Generated members use namespace `Joi.H.AppUI` conventions and remain compatible
with IL2CPP because no runtime code generation or dynamic proxy is used.

Before scanning, the operation freezes the built-in UGUI rules plus every
provider ID explicitly selected by `UIBindingSettings.EnabledRuleProviderIds`.
Generation, binding and validation must use that same snapshot. Optional
providers never become active merely because their assembly is installed.

## Stage 2: bind

The prefab binder writes serialized references into the controller only after
generation succeeds. Ownership and variant validators then ensure:

- one valid controller owns each page scope;
- required bindings exist and have compatible component types;
- nested groups do not leak ownership into the parent;
- prefab variants do not silently replace required references;
- generated and serialized member sets stay aligned.
- selected Provider IDs resolve without RuleId or component-type conflicts.

## Validation commands

- `Tools/Joi.H AppUI/Binding Validation`
- `Tools/Joi.H AppUI/Validate Input Policies`
- `Tools/Joi.H AppUI/Validate Focus P0`
- `Tools/Joi.H AppUI/Open Focus Runtime Trace`

Validation is read-only. The build preprocessor can fail a build when binding or
critical definition errors remain; it must never generate, save, or repair
assets implicitly.

## Consumer boundaries

Generated controllers may call application services through context-owned
interfaces, but package code and generated base APIs must not reference the
application's service implementations, page IDs, localization tables, or
resource handles.
