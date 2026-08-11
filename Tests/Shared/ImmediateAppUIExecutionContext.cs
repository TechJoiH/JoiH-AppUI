using System;

namespace Joi.H.AppUI.Tests
{
    public sealed class ImmediateAppUIExecutionContext :
        IAppUIExecutionContext
    {
        public bool IsCurrent
        {
            get { return true; }
        }

        public void Post(Action continuation)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            continuation.Invoke();
        }
    }
}
