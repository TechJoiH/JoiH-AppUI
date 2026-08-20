using System;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI.Validation.Consumer
{
    [DisallowMultipleComponent]
    public sealed class ConsumerNoticeView : NoticeViewBase
    {
        [SerializeField]
        private Text label;

        public override void ApplyContent(in UINoticeContent content)
        {
            if (label == null)
            {
                throw new InvalidOperationException(
                    "ConsumerNoticeView requires an authored UGUI Text.");
            }

            label.text = content.Text;
            label.color = content.Color;
            if (content.FontSize > 0f) label.fontSize = Mathf.RoundToInt(content.FontSize);
        }
    }
}
