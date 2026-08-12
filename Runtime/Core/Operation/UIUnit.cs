namespace Joi.H.AppUI
{
    /// <summary>
    /// Explicit success value for operations that have no domain payload.
    /// </summary>
    public readonly struct UIUnit
    {
        public static readonly UIUnit Value = new UIUnit();
    }
}
