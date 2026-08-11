using System;
using System.Reflection;
using Joi.H.AppUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Prefab 写操作前后的保存辅助工具。
    /// 负责在 Prefab Mode 中复用 Unity 原生 Save/Discard/Cancel 弹窗，避免对未保存或已重载对象生成文件/写引用。
    /// </summary>
    public static class UIBindingPrefabSaveUtility
    {
        private const string SaveModifiedPrefabStagesMethodName =
            "SaveCurrentModifiedPrefabStagesIfUserWantsTo";

        private static MethodInfo saveModifiedPrefabStagesMethod;

        /// <summary>
        /// 在任何绑定写操作前检查当前 Prefab Mode。
        /// 返回 false 表示用户取消、保存失败或对象已失效，调用方必须立即停止写入。
        /// </summary>
        public static bool TrySaveCurrentPrefabModeBeforeWrite(
            UIBindingScopeBase scope,
            out string error)
        {
            error = string.Empty;
            if (scope == null)
            {
                error = "Binding scope is null.";
                return false;
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                return true;
            }

            GameObject scopeObject = scope.gameObject;
            if (scopeObject == null)
            {
                error = "Binding scope GameObject is missing.";
                return false;
            }

            if (!prefabStage.IsPartOfPrefabContents(scopeObject))
            {
                return true;
            }

            // 只处理当前 Prefab Stage 内的对象；普通场景 prefab instance 不触发这套保存流程。
            MethodInfo method = GetSaveModifiedPrefabStagesMethod();
            if (method == null)
            {
                error =
                    "Cannot locate Unity Prefab Mode save API: " +
                    SaveModifiedPrefabStagesMethodName;
                return false;
            }

            bool canContinue;
            try
            {
                // Unity 内部 API 会显示原生保存弹窗，并返回是否允许继续。
                object result = method.Invoke(null, null);
                if (!(result is bool))
                {
                    error =
                        "Unity Prefab Mode save API returned an unexpected result: " +
                        SaveModifiedPrefabStagesMethodName;
                    return false;
                }

                canContinue = (bool)result;
            }
            catch (TargetInvocationException exception)
            {
                Exception inner = exception.InnerException ?? exception;
                error = "Prefab Mode save failed before binding write: " + inner.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = "Prefab Mode save failed before binding write: " + exception.Message;
                return false;
            }

            if (!canContinue)
            {
                error = "Prefab Mode changes were not saved. Binding generation/writeback stopped.";
                return false;
            }

            if (scope == null || scope.gameObject == null)
            {
                error =
                    "Prefab Mode contents were reloaded. Select the Controller again before generating bindings.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 写回引用后保存 Prefab/Asset。
        /// 该方法只在显式写回成功后调用，Validate All 等只读流程不得调用。
        /// </summary>
        public static void Save(UIBindingScopeBase scope)
        {
            if (scope == null)
            {
                return;
            }

            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(scope.gameObject);
            if (root != null)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(scope);
            }

            if (PrefabUtility.IsPartOfPrefabAsset(scope.gameObject))
            {
                PrefabUtility.SavePrefabAsset(scope.gameObject);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 通过反射缓存 Unity Prefab Mode 的内部保存 API；仅 Editor 工具使用，不进入运行时。
        /// </summary>
        private static MethodInfo GetSaveModifiedPrefabStagesMethod()
        {
            if (saveModifiedPrefabStagesMethod != null)
            {
                return saveModifiedPrefabStagesMethod;
            }

            saveModifiedPrefabStagesMethod =
                typeof(PrefabStageUtility).GetMethod(
                    SaveModifiedPrefabStagesMethodName,
                    BindingFlags.Static | BindingFlags.NonPublic);
            return saveModifiedPrefabStagesMethod;
        }
    }
}
