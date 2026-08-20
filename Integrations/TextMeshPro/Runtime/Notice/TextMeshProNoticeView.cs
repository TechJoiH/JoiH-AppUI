using System;
using TMPro;
using UnityEngine;

namespace Joi.H.AppUI.Integrations.TextMeshPro
{
    [DisallowMultipleComponent]
    public sealed class TextMeshProNoticeView : NoticeViewBase
    {
        [SerializeField]
        private TMP_Text label;

        public override void ApplyContent(in UINoticeContent content)
        {
            if (label == null)
            {
                throw new InvalidOperationException(
                    "TextMeshProNoticeView requires an authored TMP_Text.");
            }

            label.text = content.Text;
            label.color = content.Color;
            if (content.FontSize > 0f) label.fontSize = content.FontSize;
        }
    }
}
