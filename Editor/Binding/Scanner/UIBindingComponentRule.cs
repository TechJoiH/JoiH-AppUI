using System;

namespace Joi.H.AppUI.Editor.Binding
{
    /// <summary>Immutable component matching rule contributed by the built-in set or an optional Provider.</summary>
    public sealed class UIBindingComponentRule
    {
        public UIBindingComponentRule(
            string ruleId,
            Type componentType,
            string fieldSuffix,
            string codeTypeName,
            UIBindingTargetKind targetKind,
            int functionPriority,
            bool allowImplicitSelect)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                throw new ArgumentException("Binding rule ID cannot be empty.", nameof(ruleId));
            }

            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (string.IsNullOrWhiteSpace(fieldSuffix))
            {
                throw new ArgumentException("Binding rule field suffix cannot be empty.", nameof(fieldSuffix));
            }

            if (targetKind != UIBindingTargetKind.BindingScope && string.IsNullOrWhiteSpace(codeTypeName))
            {
                throw new ArgumentException(
                    "Component binding rules require an explicit generated code type name.",
                    nameof(codeTypeName));
            }

            RuleId = ruleId;
            ComponentType = componentType;
            FieldSuffix = fieldSuffix;
            CodeTypeName = codeTypeName;
            TargetKind = targetKind;
            FunctionPriority = functionPriority;
            AllowImplicitSelect = allowImplicitSelect;
        }

        public string RuleId { get; }
        public Type ComponentType { get; }
        public string FieldSuffix { get; }
        public string CodeTypeName { get; }
        public UIBindingTargetKind TargetKind { get; }
        public int FunctionPriority { get; }
        public bool AllowImplicitSelect { get; }
    }
}
