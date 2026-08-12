namespace Joi.H.AppUI.Validation.Consumer
{
    public partial class ConsumerPopupController :
        PanelBaseController,
        IUICancelHandler
    {
        public static int CancelCount { get; private set; }

        public bool HandleCancel()
        {
            CancelCount++;
            return false;
        }

        public static void ResetDiagnostics()
        {
            CancelCount = 0;
        }
    }
}
