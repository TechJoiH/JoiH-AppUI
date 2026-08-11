using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Game View 只读焦点诊断层。只在 Editor/Development 且对应 Scope 开启 Trace 时绘制。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppUIFocusDebugOverlay : MonoBehaviour
    {
        private const float PanelWidth = 620f;
        private const float PanelHeight = 132f;
        private readonly Vector3[] worldCorners = new Vector3[4];
        private readonly StringBuilder textBuilder = new StringBuilder(512);
        private long pageInstanceId;
        private GUIStyle panelStyle;
        private GUIStyle textStyle;

        internal void Configure(long instanceId)
        {
            pageInstanceId = instanceId;
            hideFlags |= HideFlags.DontSave;
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Application.isPlaying || Event.current.type != EventType.Repaint)
            {
                return;
            }

            bool hasSnapshot = pageInstanceId > 0
                ? AppUIFocusTrace.TryGetSnapshot(pageInstanceId, out AppUIFocusDebugSnapshot snapshot)
                : AppUIFocusTrace.TryGetLatestSnapshot(out snapshot);
            if (!hasSnapshot)
            {
                return;
            }

            EnsureStyles();
            DrawFocusedRect();
            textBuilder.Clear();
            textBuilder.Append("Focus Trace | Page=")
                .Append(snapshot.PageId)
                .Append(" Scope=")
                .Append(snapshot.ScopeId)
                .Append(" Status=")
                .Append(snapshot.ScopeStatus)
                .Append(" Region=")
                .AppendLine(snapshot.ActiveRegionId)
                .Append("Current=")
                .Append(snapshot.Current)
                .Append(" Order=")
                .Append(snapshot.CurrentOrder)
                .Append(" | Last=")
                .AppendLine(snapshot.Last.ToString())
                .Append("Candidates: ")
                .AppendLine(snapshot.Candidates);
            if (AppUIFocusTrace.TryGetLatestEntry(
                    snapshot.PageInstanceId,
                    out AppUIFocusTraceEntry latest))
            {
                textBuilder.Append("Last Event: ").Append(latest);
            }

            Color previousPanelColor = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
            GUI.Box(
                new Rect(8f, 8f, PanelWidth, PanelHeight),
                GUIContent.none,
                panelStyle);
            GUI.color = previousPanelColor;
            GUI.Label(
                new Rect(16f, 14f, PanelWidth - 16f, PanelHeight - 12f),
                textBuilder.ToString(),
                textStyle);
#endif
        }

        private void DrawFocusedRect()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            if (selected == null ||
                !(selected.transform is RectTransform rectTransform))
            {
                return;
            }

            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera camera = canvas != null &&
                            canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            rectTransform.GetWorldCorners(worldCorners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(
                camera,
                worldCorners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(
                camera,
                worldCorners[2]);
            Rect rect = new Rect(
                bottomLeft.x,
                Screen.height - topRight.y,
                topRight.x - bottomLeft.x,
                topRight.y - bottomLeft.y);
            Color previousColor = GUI.color;
            GUI.color = Color.yellow;
            GUI.Box(new Rect(rect.x, rect.y, rect.width, 2f), GUIContent.none);
            GUI.Box(
                new Rect(rect.x, rect.yMax - 2f, rect.width, 2f),
                GUIContent.none);
            GUI.Box(new Rect(rect.x, rect.y, 2f, rect.height), GUIContent.none);
            GUI.Box(
                new Rect(rect.xMax - 2f, rect.y, 2f, rect.height),
                GUIContent.none);
            GUI.color = previousColor;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.textColor = Color.white;
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
            };
            textStyle.normal.textColor = Color.white;
        }
    }
}
