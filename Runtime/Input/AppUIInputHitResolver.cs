using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    public sealed class AppUIInputHitResolver
    {
        public static readonly AppUIInputHitResolver Shared = new AppUIInputHitResolver();

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(32);
        private readonly List<AppUIInputZone> zoneBuffer = new List<AppUIInputZone>(8);

        private EventSystem cachedEventSystem;
        private PointerEventData pointerEventData;

        public bool IsPointerBlocked(
            Vector2 screenPosition,
            AppUIInputChannel channel)
        {
            return TryGetFirstBlocker(screenPosition, channel, out _);
        }

        public bool TryGetFirstBlocker(
            Vector2 screenPosition,
            AppUIInputChannel channel,
            out GameObject blocker)
        {
            blocker = null;
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || channel == AppUIInputChannel.None)
            {
                return false;
            }

            EnsurePointerEventData(eventSystem);
            pointerEventData.Reset();
            pointerEventData.position = screenPosition;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);

            bool blocked = false;
            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject target = raycastResults[i].gameObject;
                if (target == null)
                {
                    continue;
                }

                if (BlocksChannel(raycastResults[i], screenPosition, channel))
                {
                    blocker = target;
                    blocked = true;
                    break;
                }
            }

            raycastResults.Clear();
            return blocked;
        }

        private void EnsurePointerEventData(EventSystem eventSystem)
        {
            if (pointerEventData == null || cachedEventSystem != eventSystem)
            {
                cachedEventSystem = eventSystem;
                pointerEventData = new PointerEventData(eventSystem);
            }
        }

        private bool BlocksChannel(
            RaycastResult hit,
            Vector2 screenPosition,
            AppUIInputChannel channel)
        {
            GameObject target = hit.gameObject;
            bool isInteractiveSelectable = HasInteractiveSelectable(target);
            AppUIInputZone zone = target.GetComponentInParent<AppUIInputZone>(true);
            if (zone != null && zone.Mode != AppUIInputZoneMode.Inherit)
            {
                return zone.Blocks(channel, isInteractiveSelectable);
            }

            AppUIInputPolicyRoot policyRoot = target.GetComponentInParent<AppUIInputPolicyRoot>(true);
            if (policyRoot != null)
            {
                AppUIInputZone containingZone = FindContainingZone(
                    policyRoot,
                    screenPosition,
                    hit.module == null ? null : hit.module.eventCamera);
                if (containingZone != null)
                {
                    return containingZone.Blocks(channel, isInteractiveSelectable);
                }

                return policyRoot.Blocks(channel, isInteractiveSelectable);
            }

            return target.GetComponentInParent<Canvas>(true) != null;
        }

        private AppUIInputZone FindContainingZone(
            AppUIInputPolicyRoot policyRoot,
            Vector2 screenPosition,
            Camera eventCamera)
        {
            zoneBuffer.Clear();
            policyRoot.GetComponentsInChildren(false, zoneBuffer);

            AppUIInputZone result = null;
            for (int i = 0; i < zoneBuffer.Count; i++)
            {
                AppUIInputZone zone = zoneBuffer[i];
                if (zone == null ||
                    !zone.isActiveAndEnabled ||
                    zone.Mode == AppUIInputZoneMode.Inherit)
                {
                    continue;
                }

                RectTransform rectTransform = zone.transform as RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                Camera camera = ResolveEventCamera(rectTransform, eventCamera);
                if (RectTransformUtility.RectangleContainsScreenPoint(
                    rectTransform,
                    screenPosition,
                    camera))
                {
                    result = zone;
                }
            }

            zoneBuffer.Clear();
            return result;
        }

        private static Camera ResolveEventCamera(RectTransform rectTransform, Camera fallbackCamera)
        {
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>(true);
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return fallbackCamera != null ? fallbackCamera : canvas.worldCamera;
        }

        private static bool HasInteractiveSelectable(GameObject target)
        {
            Selectable selectable = target.GetComponentInParent<Selectable>(true);
            return selectable != null &&
                selectable.IsActive() &&
                selectable.IsInteractable();
        }

    }
}
