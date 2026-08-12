using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// A page transition is either synchronous or backed by a host operation.
    /// Immediate is a lifecycle branch, not an asynchronous implementation.
    /// </summary>
    public readonly struct UITransition
    {
        private readonly IUIOperation<UITransitionResult> operation;

        private UITransition(IUIOperation<UITransitionResult> operation)
        {
            this.operation = operation;
        }

        public static UITransition Immediate
        {
            get { return default; }
        }

        public bool IsImmediate
        {
            get { return operation == null; }
        }

        public IUIOperation<UITransitionResult> Operation
        {
            get { return operation; }
        }

        public static UITransition WaitFor(
            IUIOperation<UITransitionResult> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            return new UITransition(operation);
        }
    }
}
