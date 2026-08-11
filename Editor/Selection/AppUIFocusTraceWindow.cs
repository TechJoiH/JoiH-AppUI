#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor
{
    public sealed class AppUIFocusTraceWindow : EditorWindow
    {
        private const string MenuPath =
            "Tools/Joi.H AppUI/Open Focus Runtime Trace";

        private readonly List<AppUIFocusTraceEntry> entries =
            new List<AppUIFocusTraceEntry>(AppUIFocusTrace.Capacity);

        private double nextRefreshTime;
        private Vector2 scrollPosition;

        [MenuItem(MenuPath, false, 2020)]
        public static void OpenWindow()
        {
            AppUIFocusTraceWindow window =
                GetWindow<AppUIFocusTraceWindow>("AppUI Focus Trace");
            window.minSize = new Vector2(760f, 360f);
            window.RefreshNow();
            window.Show();
        }

        private void RefreshNow()
        {
            AppUIFocusTrace.CopyEntries(entries);
            Repaint();
        }

        private void ClearTrace()
        {
            AppUIFocusTrace.Clear();
            RefreshNow();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.timeSinceStartup < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = EditorApplication.timeSinceStartup + 0.2d;
            RefreshNow();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    RefreshNow();
                }

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                {
                    ClearTrace();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(entries.Count + " / " + AppUIFocusTrace.Capacity);
            }

            EditorGUILayout.LabelField(
                "Entries (oldest to newest)",
                EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < entries.Count; i++)
            {
                EditorGUILayout.SelectableLabel(
                    entries[i].ToString(),
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            EditorGUILayout.EndScrollView();
        }
    }

    [CustomEditor(typeof(AppUIFocusAuthoring))]
    public sealed class AppUIFocusAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(8f);

            AppUIFocusAuthoring authoring = (AppUIFocusAuthoring)target;
            if (GUILayout.Button("Validate Focus", GUILayout.Height(26f)))
            {
                authoring.ValidateFocus();
            }

            if (GUILayout.Button("Print Focus Map", GUILayout.Height(26f)))
            {
                authoring.PrintFocusMap();
            }

            if (GUILayout.Button("Open Runtime Trace", GUILayout.Height(30f)))
            {
                AppUIFocusTraceWindow.OpenWindow();
            }
        }
    }
}
#endif
