using System;
using System.Collections.Generic;
using UnityEditor;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 查找 App UI 绑定设置资产的工具。
    /// Editor Window 继续使用宽松查找；CI 入口使用显式路径或唯一资产，避免机器环境取到错误设置。
    /// </summary>
    public static class UIBindingSettingsUtility
    {
        /// <summary>
        /// 在工程中查找第一个 UIBindingSettings。
        /// 该方法保留给现有 Editor Window/Inspector 使用；CI 不应依赖“第一个”的不稳定顺序。
        /// </summary>
        public static UIBindingSettings FindSettings()
        {
            string[] paths = FindSettingsPaths();
            if (paths.Length == 0)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<UIBindingSettings>(paths[0]);
        }

        /// <summary>
        /// 查找工程内所有 UIBindingSettings 的 Asset 路径，并按路径排序保证命令行结果稳定。
        /// 这里只读取 AssetDatabase，不创建、不保存任何资产。
        /// </summary>
        public static string[] FindSettingsPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:UIBindingSettings");
            if (guids == null || guids.Length == 0)
            {
                return new string[0];
            }

            List<string> paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths.ToArray();
        }

        /// <summary>
        /// 按显式 Asset 路径加载 UIBindingSettings。
        /// 命令行入口使用该方法锁定 CI 配置，避免新增测试 Settings 后误跑另一套 Registry。
        /// </summary>
        public static bool TryLoadSettingsAtPath(
            string assetPath,
            out UIBindingSettings settings,
            out string normalizedPath,
            out string error)
        {
            settings = null;
            normalizedPath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrEmpty(assetPath))
            {
                error = "UIBindingSettings path is empty.";
                return false;
            }

            normalizedPath = assetPath.Replace('\\', '/');
            settings = AssetDatabase.LoadAssetAtPath<UIBindingSettings>(normalizedPath);
            if (settings == null)
            {
                error = "Cannot load UIBindingSettings at path: " + normalizedPath;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 在未传入路径时查找唯一的 UIBindingSettings。
        /// 没有或存在多个都返回失败，让 CI 配置显式暴露问题，而不是悄悄选择错误设置。
        /// </summary>
        public static bool TryFindUniqueSettings(
            out UIBindingSettings settings,
            out string settingsPath,
            out string error)
        {
            settings = null;
            settingsPath = string.Empty;
            error = string.Empty;

            string[] paths = FindSettingsPaths();
            if (paths.Length == 0)
            {
                error = "No UIBindingSettings asset was found. Pass -appUIBindingSettingsPath or create one settings asset.";
                return false;
            }

            if (paths.Length > 1)
            {
                error =
                    "Multiple UIBindingSettings assets were found. Select one in the Definition sync window or pass -appUIBindingSettingsPath for command line validation: " +
                    string.Join(", ", paths);
                return false;
            }

            settingsPath = paths[0];
            settings = AssetDatabase.LoadAssetAtPath<UIBindingSettings>(settingsPath);
            if (settings == null)
            {
                error = "Cannot load the only UIBindingSettings asset: " + settingsPath;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 解析命令行使用的 Settings：显式路径优先；没有路径时要求工程内只有一个 Settings。
        /// </summary>
        public static bool TryResolveSettingsForCommandLine(
            string assetPath,
            out UIBindingSettings settings,
            out string settingsPath,
            out string error)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                return TryLoadSettingsAtPath(assetPath, out settings, out settingsPath, out error);
            }

            return TryFindUniqueSettings(out settings, out settingsPath, out error);
        }
    }
}
