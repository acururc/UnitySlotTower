using UnityEngine;

namespace UnitySlotTower.Ui
{
    public static class UiStyle
    {
        public static readonly Color Danger = new Color(224f / 255f, 69f / 255f, 58f / 255f);
        public static readonly Color Ally = new Color(63f / 255f, 163f / 255f, 232f / 255f);
        public static readonly Color Ready = new Color(242f / 255f, 194f / 255f, 48f / 255f);
        public static readonly Color Yen = new Color(0.36f, 0.78f, 0.42f);
        public static readonly Color Coin = new Color(0.98f, 0.80f, 0.20f);
        public static readonly Color PanelFill = new Color(0.05f, 0.06f, 0.08f, 0.72f);
        public static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.38f);
        // 戦闘以外の画面（T387）: 画面の地・帯・押せる札やボタン・枠だけの区画。
        public static readonly Color ScreenFill = new Color(0.063f, 0.071f, 0.094f, 1f);
        public static readonly Color BandFill = new Color(0.03f, 0.035f, 0.05f, 0.92f);
        public static readonly Color ButtonFill = new Color(0.15f, 0.165f, 0.21f, 1f);
        public static readonly Color SectionFill = new Color(0.1f, 0.11f, 0.14f, 1f);
        public static readonly Color OnReady = new Color(0.08f, 0.08f, 0.1f, 1f);
        public const float BorderWidth = 3f;
        public const float CornerCut = 16f;
        public static readonly Color TextColor = Color.white;
        public static readonly Color DimText = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color DisabledTint = new Color(0.42f, 0.42f, 0.45f, 1f);
        public static readonly Color CooldownTint = new Color(0.6f, 0.45f, 0.45f, 1f);
    }
}
