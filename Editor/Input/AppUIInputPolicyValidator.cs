#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI.Editor
{
    internal static class AppUIInputPolicyValidator
    {
        [MenuItem("Tools/Joi.H AppUI/Validate Input Policies")]
        public static void ValidateAll()
        {
            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets" });
            int appUIPrefabCount = 0;
            int errorCount = 0;
            int warningCount = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null ||
                    (prefab.GetComponent<PanelBaseController>() == null &&
                     prefab.GetComponent<AppUIInputPolicyRoot>() == null))
                {
                    continue;
                }

                appUIPrefabCount++;
                ValidatePrefab(
                    prefab,
                    path,
                    ref errorCount,
                    ref warningCount);
            }

            Debug.Log(
                "<AppUIInputPolicyValidator> Completed. " +
                $"AppUIPrefabs={appUIPrefabCount}, " +
                $"Errors={errorCount}, Warnings={warningCount}");
        }

        private static void ValidatePrefab(
            GameObject prefab,
            string path,
            ref int errorCount,
            ref int warningCount)
        {
            PanelBaseController panel = prefab.GetComponent<PanelBaseController>();
            bool hasStrictPolicy =
                AppUIInputPolicyPageValidatorRegistry.HasStrictPolicy(prefab);
            if (panel != null &&
                !hasStrictPolicy &&
                prefab.GetComponent<AppUIInputPolicyRoot>() == null)
            {
                warningCount++;
                Debug.LogWarning(
                    "<AppUIInputPolicyValidator> AppUI panel has no " +
                    $"AppUIInputPolicyRoot. Path={path}",
                    prefab);
            }

            AppUIInputPolicyPageValidatorRegistry.Validate(
                prefab,
                path,
                ref errorCount,
                ref warningCount);
            ValidateSelectableChildRaycastTargets(
                prefab,
                path,
                ref warningCount);
        }

        private static void ValidateSelectableChildRaycastTargets(
            GameObject prefab,
            string path,
            ref int warningCount)
        {
            Graphic[] graphics = prefab.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null || !graphic.raycastTarget)
                {
                    continue;
                }

                Selectable selectable =
                    graphic.GetComponentInParent<Selectable>(true);
                if (selectable == null ||
                    selectable.targetGraphic == graphic ||
                    selectable.gameObject == graphic.gameObject)
                {
                    continue;
                }

                warningCount++;
                Debug.LogWarning(
                    "<AppUIInputPolicyValidator> Selectable child Graphic has " +
                    $"raycastTarget=true. Path={path}, Graphic=" +
                    AppUIInputPolicyEditorUtility.GetHierarchyPath(
                        graphic.transform),
                    graphic);
            }
        }
    }
}
#endif
