using System.Collections.Generic;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 绑定引用写回的结果对象，收集错误与提示信息，供 Inspector 按钮和日志统一展示。
    /// </summary>
    public sealed class UIBindingBindResult
    {
        private readonly List<string> errors = new List<string>(4);
        private readonly List<string> infos = new List<string>(4);

        /// <summary>
        /// 是否写回成功；只要存在任意错误，就视为失败并停止后续依赖流程。
        /// </summary>
        public bool Success
        {
            get { return errors.Count == 0; }
        }

        /// <summary>
        /// 写回过程中发现的错误列表。
        /// </summary>
        public IReadOnlyList<string> Errors
        {
            get { return errors; }
        }

        /// <summary>
        /// 写回过程中产生的提示列表。
        /// </summary>
        public IReadOnlyList<string> Infos
        {
            get { return infos; }
        }

        /// <summary>
        /// 追加错误；调用方只负责收集，不在这里抛异常，避免 Editor 操作链中断在半写入状态。
        /// </summary>
        public void AddError(string error)
        {
            errors.Add(error ?? string.Empty);
        }

        /// <summary>
        /// 追加提示信息，用于说明已完成的自动修复或后续人工动作。
        /// </summary>
        public void AddInfo(string info)
        {
            infos.Add(info ?? string.Empty);
        }
    }
}
