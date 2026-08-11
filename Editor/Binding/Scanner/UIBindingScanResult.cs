using System.Collections.Generic;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 一次 Scope 扫描的结果，包含可生成绑定、错误和提示。
    /// </summary>
    public sealed class UIBindingScanResult
    {
        private readonly List<UIBindingInfo> bindings = new List<UIBindingInfo>(16);
        private readonly List<string> errors = new List<string>(4);
        private readonly List<string> infos = new List<string>(4);

        /// <summary>
        /// 成功收集到的绑定列表。
        /// </summary>
        public IReadOnlyList<UIBindingInfo> Bindings
        {
            get { return bindings; }
        }

        /// <summary>
        /// 扫描过程中的错误列表。
        /// </summary>
        public IReadOnlyList<string> Errors
        {
            get { return errors; }
        }

        /// <summary>
        /// 扫描过程中的提示列表。
        /// </summary>
        public IReadOnlyList<string> Infos
        {
            get { return infos; }
        }

        /// <summary>
        /// 是否存在阻断生成/写回的扫描错误。
        /// </summary>
        public bool HasError
        {
            get { return errors.Count > 0; }
        }

        /// <summary>
        /// 追加一条有效绑定。
        /// </summary>
        public void AddBinding(UIBindingInfo binding)
        {
            bindings.Add(binding);
        }

        /// <summary>
        /// 追加扫描错误。
        /// </summary>
        public void AddError(string error)
        {
            errors.Add(error ?? string.Empty);
        }

        /// <summary>
        /// 追加扫描提示。
        /// </summary>
        public void AddInfo(string info)
        {
            infos.Add(info ?? string.Empty);
        }
    }
}
