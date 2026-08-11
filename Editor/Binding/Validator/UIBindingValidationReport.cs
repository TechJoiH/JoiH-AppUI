using System.Collections.Generic;
using System.Text;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// Editor 绑定校验报告，统一承载错误和提示。
    /// </summary>
    public sealed class UIBindingValidationReport
    {
        private readonly List<string> errors = new List<string>(8);
        private readonly List<string> warnings = new List<string>(8);
        private readonly List<string> infos = new List<string>(8);

        /// <summary>
        /// 校验错误列表。
        /// </summary>
        public IReadOnlyList<string> Errors
        {
            get { return errors; }
        }

        /// <summary>
        /// 校验提示列表。
        /// </summary>
        public IReadOnlyList<string> Infos
        {
            get { return infos; }
        }

        /// <summary>
        /// 校验警告列表；不阻断构建，但会进入窗口、Console 和 CI JSON。
        /// </summary>
        public IReadOnlyList<string> Warnings
        {
            get { return warnings; }
        }

        /// <summary>
        /// 是否存在错误。
        /// </summary>
        public bool HasError
        {
            get { return errors.Count > 0; }
        }

        /// <summary>
        /// 追加错误信息。
        /// </summary>
        public void AddError(string message)
        {
            errors.Add(message ?? string.Empty);
        }

        /// <summary>
        /// 追加提示信息。
        /// </summary>
        public void AddInfo(string message)
        {
            infos.Add(message ?? string.Empty);
        }

        /// <summary>
        /// 追加非阻断警告信息。
        /// </summary>
        public void AddWarning(string message)
        {
            warnings.Add(message ?? string.Empty);
        }

        /// <summary>
        /// 生成 Console/窗口可读文本。
        /// </summary>
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(1024);
            for (int i = 0; i < errors.Count; i++)
            {
                builder.Append("[error] ").AppendLine(errors[i]);
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                builder.Append("[warning] ").AppendLine(warnings[i]);
            }

            for (int i = 0; i < infos.Count; i++)
            {
                builder.Append("[info] ").AppendLine(infos[i]);
            }

            return builder.ToString();
        }
    }
}
