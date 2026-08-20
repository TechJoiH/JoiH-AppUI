using UnityEngine;

namespace Joi.H.AppUI
{
    /// <summary>
    /// Technology-neutral content passed from NoticeService to an authored view.
    /// </summary>
    public readonly struct UINoticeContent
    {
        public UINoticeContent(
            string text,
            Color color,
            float fontSize)
        {
            Text = text ?? string.Empty;
            Color = color;
            FontSize = Mathf.Max(0f, fontSize);
        }

        public string Text { get; }

        public Color Color { get; }

        public float FontSize { get; }

        public static UINoticeContent Empty
        {
            get
            {
                return new UINoticeContent(
                    string.Empty,
                    Color.white,
                    0f);
            }
        }
    }
}
