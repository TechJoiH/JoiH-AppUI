namespace Joi.H.AppUI
{
    public abstract class UIFlowContextBase
    {
        protected UIFlowContextBase(
            IUIControllerService ui,
            IUIFlowCoordinator flow,
            IUIPageFlowContractRegistry contracts,
            IUILocalizationService localization,
            string sceneScopeId)
        {
            UI = ui;
            Flow = flow;
            Contracts = contracts;
            Localization = localization;
            SceneScopeId = sceneScopeId ?? string.Empty;
        }

        public IUIControllerService UI { get; private set; }
        public IUIFlowCoordinator Flow { get; private set; }
        public IUIPageFlowContractRegistry Contracts { get; private set; }
        public IUILocalizationService Localization { get; private set; }
        public string SceneScopeId { get; private set; }

        /// <summary>
        /// ReplacePage 关闭来源页时是否释放实例。默认释放；需要在同一场景
        /// 生命周期内恢复页面状态与焦点历史的流程可以覆写为 false。
        /// </summary>
        public virtual bool ReleaseCurrentPageOnReplace
        {
            get { return true; }
        }
    }
}
