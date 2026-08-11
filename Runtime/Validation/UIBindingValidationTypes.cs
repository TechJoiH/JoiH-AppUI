using System.Collections.Generic;
using System.Text;

namespace Joi.H.AppUI
{
    /// <summary>绑定校验消息级别。</summary>
    public enum UIBindingValidationLevel
    {
        Info,
        Error,
    }

    /// <summary>
    /// 单条绑定校验消息。
    /// 记录级别、成员名、期望类型和面向用户的提示信息。
    /// </summary>
    public sealed class UIBindingValidationMessage
    {
        public UIBindingValidationLevel Level { get; private set; }
        public string MemberName { get; private set; }
        public string ExpectedType { get; private set; }
        public string Message { get; private set; }

        public UIBindingValidationMessage(
            UIBindingValidationLevel level,
            string memberName,
            string expectedType,
            string message)
        {
            Level = level;
            MemberName = memberName ?? string.Empty;
            ExpectedType = expectedType ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// 绑定校验结果集合。
    /// Controller 自定义校验、Inspector 校验和 Validate All 都使用该类型汇总消息。
    /// </summary>
    public sealed class UIBindingValidationResult
    {
        private readonly List<UIBindingValidationMessage> messages =
            new List<UIBindingValidationMessage>(4);

        /// <summary>当前所有校验消息。</summary>
        public IReadOnlyList<UIBindingValidationMessage> Messages
        {
            get { return messages; }
        }

        /// <summary>是否存在 Error 级别消息。</summary>
        public bool HasError { get; private set; }

        /// <summary>添加信息级消息。</summary>
        public void AddInfo(string message)
        {
            Add(UIBindingValidationLevel.Info, string.Empty, string.Empty, message);
        }

        /// <summary>添加错误级消息。</summary>
        public void AddError(string message)
        {
            Add(UIBindingValidationLevel.Error, string.Empty, string.Empty, message);
        }

        /// <summary>添加必需绑定缺失错误。</summary>
        public void AddMissing(string memberName, string expectedType)
        {
            Add(
                UIBindingValidationLevel.Error,
                memberName,
                expectedType,
                "Required UI binding is missing.");
        }

        /// <summary>添加指定级别的校验消息。</summary>
        public void Add(
            UIBindingValidationLevel level,
            string memberName,
            string expectedType,
            string message)
        {
            if (level == UIBindingValidationLevel.Error)
            {
                HasError = true;
            }

            messages.Add(new UIBindingValidationMessage(level, memberName, expectedType, message));
        }

        public override string ToString()
        {
            if (messages.Count == 0)
            {
                return "No binding validation messages.";
            }

            StringBuilder builder = new StringBuilder(messages.Count * 64);
            for (int i = 0; i < messages.Count; i++)
            {
                UIBindingValidationMessage message = messages[i];
                builder.Append('[').Append(message.Level).Append("] ");
                if (!string.IsNullOrEmpty(message.MemberName))
                {
                    builder.Append(message.MemberName).Append(" (").Append(message.ExpectedType).Append("): ");
                }

                builder.AppendLine(message.Message);
            }

            return builder.ToString();
        }
    }
}
