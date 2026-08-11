using System;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Marshals external completion callbacks to the Unity context selected by the host.
    /// AppUI Core intentionally provides no implementation.
    /// </summary>
    public interface IAppUIExecutionContext
    {
        bool IsCurrent { get; }

        void Post(Action continuation);
    }
}
