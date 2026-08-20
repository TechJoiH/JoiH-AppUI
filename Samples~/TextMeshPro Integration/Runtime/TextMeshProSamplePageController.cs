using System;
using Joi.H.AppUI;
using Joi.H.AppUI.Integrations.TextMeshPro;

namespace Joi.H.AppUI.Samples.TextMeshPro
{
    public partial class TextMeshProSamplePageController :
        PanelBaseController,
        IAppUIFocusDefinitionProvider
    {
        private TextMeshProFocusDropdownControlPolicy dropdownPolicy;
        private IDisposable dropdownBinding;

        protected override void OnInitEx()
        {
            dropdownPolicy = new TextMeshProFocusDropdownControlPolicy(
                OptionsDropdownDropdown,
                "options");
        }

        protected override void OnRefreshEx()
        {
            if (dropdownBinding == null && Context.FocusScope != null)
                dropdownBinding = dropdownPolicy.Bind(Context.FocusScope);
        }

        protected override void OnDisposeEx()
        {
            dropdownBinding?.Dispose();
            dropdownBinding = null;
        }

        public AppUIFocusDefinition BuildFocusDefinition()
        {
            return new AppUIFocusDefinitionBuilder("sample.textmeshpro")
                .AddRegion("options", AppUIFocusDefinition.RootRegionId, "options-list")
                .SetRegionCancelHandler("options", dropdownPolicy)
                .AddGroup("main")
                .AddGroup("options-list", "options")
                .AddNode("main", new AppUIFocusNodeKey("name"), NameInputInput)
                .AddNode("main", new AppUIFocusNodeKey("options"), OptionsDropdownDropdown, dropdownPolicy)
                .AddNode("main", new AppUIFocusNodeKey("first"), FirstButtonBtn, null, 0)
                .AddNode("main", new AppUIFocusNodeKey("second"), SecondButtonBtn, null, 1)
                .Build();
        }
    }
}
