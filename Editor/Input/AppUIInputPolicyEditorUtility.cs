#if UNITY_EDITOR
using UnityEngine;

namespace Joi.H.AppUI.Editor
{
    internal static class AppUIInputPolicyEditorUtility
    {
        public static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildByName(root.GetChild(i), childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
#endif
