namespace Joi.H.AppUI
{
    /// <summary>
    /// UI 绑定作用域标记接口。
    /// 扫描器通过该接口识别子 Scope 边界，防止父 Scope 跨子 Scope 绑定内部控件。
    /// </summary>
    public interface IUIBindingScope
    {
    }
}
