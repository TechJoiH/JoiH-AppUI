#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Joi.H.AppUI.Editor
{
    /// <summary>
    /// 阶段一 P0 编辑器守卫。只读扫描焦点写入口；Definition 结构由运行时 Validator 检查。
    /// </summary>
    internal static class AppUIFocusP0Validator
    {
        private enum LexicalState
        {
            Code,
            LineComment,
            BlockComment,
            String,
            VerbatimString,
            Character,
        }

        private const string ProjectAssetRoot = "Assets";

        [MenuItem("Tools/Joi.H AppUI/Validate Focus P0")]
        public static void ValidateAll()
        {
            AppUIFocusValidationReport report = ValidateProjectFocusWrites();
            for (int i = 0; i < report.Errors.Count; i++)
            {
                Debug.LogError("<AppUIFocusP0Validator> " + report.Errors[i]);
            }

            Debug.Log(
                "<AppUIFocusP0Validator> Completed. Errors=" +
                report.Errors.Count);
        }

        internal static AppUIFocusValidationReport ValidateProjectFocusWrites()
        {
            AppUIFocusValidationReport report = new AppUIFocusValidationReport();
            string[] scriptGuids = AssetDatabase.FindAssets(
                "t:MonoScript",
                new[] { ProjectAssetRoot });
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (string.IsNullOrEmpty(path) ||
                    !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ValidateSourceText(path, File.ReadAllText(path), report);
            }

            return report;
        }

        internal static void ValidateSourceText(
            string path,
            string source,
            AppUIFocusValidationReport report)
        {
            if (report == null || string.IsNullOrEmpty(source))
            {
                return;
            }

            if (source.IndexOf(
                    "Joi.H.AppUI",
                    StringComparison.Ordinal) < 0 &&
                source.IndexOf(
                    "AppUIFocus",
                    StringComparison.Ordinal) < 0)
            {
                return;
            }

            string normalizedPath = (path ?? string.Empty).Replace('\\', '/');
            string codeOnlySource = CreateCodeOnlySource(source);
            AppendPatternErrors(
                normalizedPath,
                codeOnlySource,
                "\\.\\s*SetSelectedGameObject\\s*\\(",
                "Only UIFocusCommitter may call EventSystem.SetSelectedGameObject.",
                report);
            AppendPatternErrors(
                normalizedPath,
                codeOnlySource,
                "\\.Select\\s*\\(\\s*\\)",
                "Selectable" + ".Select" + "() is not an allowed focus commit entry.",
                report);
        }

        private static void AppendPatternErrors(
            string path,
            string source,
            string pattern,
            string message,
            AppUIFocusValidationReport report)
        {
            MatchCollection matches = Regex.Matches(source, pattern);
            for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
            {
                int tokenIndex = matches[matchIndex].Index;
                int line = 1;
                for (int i = 0; i < tokenIndex; i++)
                {
                    if (source[i] == '\n')
                    {
                        line++;
                    }
                }

                report.AddError(
                    message +
                    " Path=" +
                    path +
                    ", Line=" +
                    line);
            }
        }

        private static string CreateCodeOnlySource(string source)
        {
            char[] code = source.ToCharArray();
            LexicalState state = LexicalState.Code;
            for (int i = 0; i < code.Length; i++)
            {
                char current = code[i];
                char next = i + 1 < code.Length ? code[i + 1] : '\0';
                switch (state)
                {
                    case LexicalState.Code:
                        if (current == '/' && next == '/')
                        {
                            Mask(code, i);
                            Mask(code, ++i);
                            state = LexicalState.LineComment;
                        }
                        else if (current == '/' && next == '*')
                        {
                            Mask(code, i);
                            Mask(code, ++i);
                            state = LexicalState.BlockComment;
                        }
                        else if (current == '@' && next == '"')
                        {
                            Mask(code, i);
                            Mask(code, ++i);
                            state = LexicalState.VerbatimString;
                        }
                        else if (current == '"')
                        {
                            Mask(code, i);
                            state = LexicalState.String;
                        }
                        else if (current == '\'')
                        {
                            Mask(code, i);
                            state = LexicalState.Character;
                        }

                        break;
                    case LexicalState.LineComment:
                        if (current == '\n')
                        {
                            state = LexicalState.Code;
                        }
                        else
                        {
                            Mask(code, i);
                        }

                        break;
                    case LexicalState.BlockComment:
                        if (current == '*' && next == '/')
                        {
                            Mask(code, i);
                            Mask(code, ++i);
                            state = LexicalState.Code;
                        }
                        else
                        {
                            Mask(code, i);
                        }

                        break;
                    case LexicalState.String:
                    case LexicalState.Character:
                        LexicalState escapedState = state;
                        Mask(code, i);
                        if (current == '\\' && next != '\0')
                        {
                            Mask(code, ++i);
                        }
                        else if ((escapedState == LexicalState.String && current == '"') ||
                                 (escapedState == LexicalState.Character && current == '\''))
                        {
                            state = LexicalState.Code;
                        }

                        break;
                    case LexicalState.VerbatimString:
                        Mask(code, i);
                        if (current == '"' && next == '"')
                        {
                            Mask(code, ++i);
                        }
                        else if (current == '"')
                        {
                            state = LexicalState.Code;
                        }

                        break;
                }
            }

            return new string(code);
        }

        private static void Mask(char[] code, int index)
        {
            if (code[index] != '\r' && code[index] != '\n')
            {
                code[index] = ' ';
            }
        }
    }
}
#endif
