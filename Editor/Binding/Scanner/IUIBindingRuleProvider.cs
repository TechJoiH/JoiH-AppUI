using System.Collections.Generic;

namespace Joi.H.AppUI.Editor.Binding
{
    public interface IUIBindingRuleProvider
    {
        string ProviderId { get; }
        IReadOnlyList<UIBindingComponentRule> Rules { get; }
    }
}
