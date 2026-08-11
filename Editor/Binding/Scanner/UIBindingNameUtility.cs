using System.Text;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 绑定字段命名工具，负责把节点名转换为合法 C# 标识符。
    /// </summary>
    public static class UIBindingNameUtility
    {
        /// <summary>
        /// 将原始节点名转换为 PascalCase 标识符片段，非法字符会被当作分隔符。
        /// </summary>
        public static string ToPascalIdentifier(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(raw.Length);
            bool upperNext = true;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (builder.Length == 0 && char.IsDigit(c))
                    {
                        builder.Append('_');
                    }

                    builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
                    upperNext = false;
                }
                else
                {
                    upperNext = true;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 校验字符串是否是合法 C# 标识符，避免生成无法编译的字段或属性。
        /// </summary>
        public static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (!(char.IsLetter(value[0]) || value[0] == '_'))
            {
                return false;
            }

            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
