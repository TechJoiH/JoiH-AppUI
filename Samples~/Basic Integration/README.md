# Basic Integration

1. Add `GlobalUIRoot`, `AppUIManager`, and `AppUIRuntimeHost` to the UI root.
2. Assign an `AppUIRuntimeProfile` and the required layer roots.
3. Disable `Initialize On Awake` on the host.
4. Add `SampleAppUIInstaller` and register prefab asset IDs in its list.

The sample provider demonstrates the adapter boundary. A production project can
replace it with an Addressables, AssetBundle, remote-content, or custom cache
provider without changing AppUI.
