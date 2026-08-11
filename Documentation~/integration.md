# Integration

## Composition root

`AppUIRuntimeHost` is intentionally small. It resolves the manager and layer
roots, selects values from `AppUIRuntimeProfile`, injects an asset provider, and
initializes the manager once. It does not create an EventSystem, load scenes, or
mark objects as persistent.

For a Resources-only application, keep **Use Resources Provider When Missing**
enabled. For another resource stack, disable **Initialize On Awake** and inject a
provider from the application's earlier composition root.

## Custom asset provider

```csharp
public sealed class ProjectUIAssetProvider : IUIAssetProvider
{
    public bool TryLoad<T>(
        string assetId,
        out UIAssetLoadResult<T> result)
        where T : UnityEngine.Object
    {
        result = UIAssetLoadResult<T>.Failure(
            UIAssetLoadStatus.SynchronousLoadUnsupported,
            "This provider is asynchronous only.");
        return false;
    }

    public async UniTask<UIAssetLoadResult<T>> LoadAsync<T>(string assetId)
        where T : UnityEngine.Object
    {
        ProjectHandle<T> handle = await LoadProjectAssetAsync<T>(assetId);
        if (!handle.Succeeded)
        {
            return UIAssetLoadResult<T>.Failure(
                UIAssetLoadStatus.ProviderFailed,
                handle.Error);
        }

        return UIAssetLoadResult<T>.Success(
            handle.Asset,
            new UIAssetLease(handle.Release));
    }
}
```

Notice prefabs are loaded through the optional synchronous entry point. When a
provider returns `SynchronousLoadUnsupported`, Notice falls back to its built-in
UGUI view; page loading remains asynchronous.

For non-Resources asset IDs, implement `IUIEditorAssetIdResolver` in an Editor
assembly and register it with `UIEditorAssetIdResolverRegistry.SetResolver`.
The binding sync and prefab validation tools then use the same ID convention as
the runtime provider.

## Host shutdown order

1. Stop new UI commands in the application.
2. Close or release active scene scopes.
3. Call `AppUIRuntimeHost.Shutdown()`.
4. Dispose the application provider.
5. Destroy the UI root if the application owns it per scene.

This order prevents an AppUI lease from calling into a provider that has already
been shut down.

## Generic input mapping

The package exposes `PrimaryPointer`, `SecondaryPointer`, `PointerMotion`,
`ViewportPan`, `ViewportZoom`, `ContextAction`, `Custom1`, and `Custom2`. A host
maps its input actions to these categories before calling
`AppUIInputHitResolver.Shared.IsPointerBlocked(position, channel)`.

Use `AppUIInputPolicyRoot` for the page default and nested `AppUIInputZone`
components for exceptions. Avoid adding raycast targets to decorative child
graphics; the input validator reports common accidental blockers.
