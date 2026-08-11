# Changelog

## [0.1.0-pre.1]

- Established `com.joih.appui` and namespace `Joi.H.AppUI`.
- Added provider-neutral asset loading and one-shot asset leases.
- Added `AppUIRuntimeHost` and Resources default provider.
- Replaced application-specific input actions with generic input channels.
- Replaced third-party inspectors and windows with Unity Editor implementations.
- Added EditMode and PlayMode contract suites and a Basic Integration sample.
- Added PlayMode integration coverage for page lifecycle, one-shot resource
  release, Notice fallback/scope cleanup, and interrupted late-load rejection.
- Expanded PlayMode coverage to Manager Cancel, provider-backed Notice leases,
  real EventSystem passthrough zones, and interactive Selectable blocking.
- Verified Windows x64 Mono and IL2CPP Development Player builds in the clean
  Unity 6 consumer project.
