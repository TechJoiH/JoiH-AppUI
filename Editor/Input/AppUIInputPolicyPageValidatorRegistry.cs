#if UNITY_EDITOR
using UnityEngine;

namespace Joi.H.AppUI.Editor
{
    /// <summary>
    /// Extension point for projects that need stricter prefab-specific input validation.
    /// The package intentionally ships without any product or scene-specific rules.
    /// </summary>
    internal static class AppUIInputPolicyPageValidatorRegistry
    {
        public static bool HasStrictPolicy(GameObject prefab)
        {
            return false;
        }

        public static void Validate(
            GameObject prefab,
            string path,
            ref int errorCount,
            ref int warningCount)
        {
        }
    }
}
#endif
