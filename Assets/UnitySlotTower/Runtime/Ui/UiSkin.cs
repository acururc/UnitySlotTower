using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Ui
{
    /// <summary>
    /// 四角い Image を角を斜めに切ったパネルの見た目にする（T387、plan 18.4）。Image は透明にして押す範囲としてだけ残し、
    /// 最初の子 <see cref="SkinName"/> の <see cref="UiPanel"/> が地と縁を描く。Button があればその押した色の変化も子に移す。
    /// 何度呼んでもよい（子は1つだけ）。
    /// </summary>
    public static class UiSkin
    {
        public const string SkinName = "Skin";

        public static UiPanel Apply(Image image, Color fill, Color border)
        {
            var rect = image.rectTransform;
            var skin = rect.Find(SkinName) as RectTransform;
            if (skin == null)
            {
                skin = new GameObject(SkinName, typeof(RectTransform)).GetComponent<RectTransform>();
                skin.SetParent(rect, false);
                skin.gameObject.layer = rect.gameObject.layer;
                skin.gameObject.AddComponent<UiPanel>();
            }

            skin.SetAsFirstSibling();
            skin.anchorMin = Vector2.zero;
            skin.anchorMax = Vector2.one;
            skin.pivot = new Vector2(0.5f, 0.5f);
            skin.offsetMin = Vector2.zero;
            skin.offsetMax = Vector2.zero;
            var panel = skin.GetComponent<UiPanel>();
            panel.color = fill;
            panel.SetBorder(border);
            panel.raycastTarget = false;

            image.sprite = null;
            image.color = Color.clear;
            if (image.TryGetComponent<Button>(out var button))
            {
                button.targetGraphic = panel;
            }

            return panel;
        }
    }
}
