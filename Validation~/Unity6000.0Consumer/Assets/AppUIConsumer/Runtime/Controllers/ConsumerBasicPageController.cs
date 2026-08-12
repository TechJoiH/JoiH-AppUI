namespace Joi.H.AppUI.Validation.Consumer
{
    public partial class ConsumerBasicPageController : PanelBaseController
    {
        public static int CreateCount { get; private set; }
        public static int InitCount { get; private set; }
        public static int RefreshCount { get; private set; }
        public static int ShowCount { get; private set; }
        public static int DisposeCount { get; private set; }
        public static object LastData { get; private set; }

        public static void ResetDiagnostics()
        {
            CreateCount = 0;
            InitCount = 0;
            RefreshCount = 0;
            ShowCount = 0;
            DisposeCount = 0;
            LastData = null;
        }

        protected override void OnCreateEx(UIControllerContext context)
        {
            CreateCount++;
        }

        protected override void OnInitEx()
        {
            InitCount++;
        }

        protected override void OnDataLoadEx(object data)
        {
            LastData = data;
        }

        protected override void OnRefreshEx()
        {
            RefreshCount++;
        }

        protected override void OnShowEx()
        {
            ShowCount++;
        }

        protected override void OnDisposeEx()
        {
            DisposeCount++;
        }
    }
}
