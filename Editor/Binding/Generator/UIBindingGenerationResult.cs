using System.Collections.Generic;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>
    /// 绑定代码生成结果，包含生成路径、扫描结果和用户可读消息。
    /// </summary>
    public sealed class UIBindingGenerationResult
    {
        private readonly List<string> errors = new List<string>(4);
        private readonly List<string> infos = new List<string>(4);

        /// <summary>
        /// 是否成功写出生成文件。
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// 成功时写出的 .Bindings.cs 路径。
        /// </summary>
        public string GeneratedFilePath { get; private set; }

        /// <summary>
        /// 本次生成使用的扫描结果；失败时也尽量保留，方便 Inspector 展示上下文。
        /// </summary>
        public UIBindingScanResult ScanResult { get; private set; }

        /// <summary>
        /// 生成失败原因列表。
        /// </summary>
        public IReadOnlyList<string> Errors { get { return errors; } }

        /// <summary>
        /// 生成成功或提示信息列表。
        /// </summary>
        public IReadOnlyList<string> Infos { get { return infos; } }

        /// <summary>
        /// 构造成功结果。
        /// </summary>
        public static UIBindingGenerationResult Ok(
            string generatedFilePath,
            UIBindingScanResult scanResult,
            string info)
        {
            UIBindingGenerationResult result = new UIBindingGenerationResult();
            result.Success = true;
            result.GeneratedFilePath = generatedFilePath ?? string.Empty;
            result.ScanResult = scanResult;
            result.AddInfo(info);
            return result;
        }

        /// <summary>
        /// 构造失败结果；不写文件、不刷新 AssetDatabase 的失败路径都走这里。
        /// </summary>
        public static UIBindingGenerationResult Fail(
            UIBindingScanResult scanResult,
            string error)
        {
            UIBindingGenerationResult result = new UIBindingGenerationResult();
            result.ScanResult = scanResult;
            result.AddError(error);
            return result;
        }

        /// <summary>
        /// 追加错误信息。
        /// </summary>
        public void AddError(string error)
        {
            errors.Add(error ?? string.Empty);
        }

        /// <summary>
        /// 追加提示信息。
        /// </summary>
        public void AddInfo(string info)
        {
            infos.Add(info ?? string.Empty);
        }
    }
}
