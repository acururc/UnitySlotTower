using TMPro;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// ブロック数の数字（札・詳細の板・向き選び: T385、FR-072）。止められない職（0）は赤い × にして「止めない」ことを見せる
    /// （足止めの説明の絵と同じ印）。
    /// </summary>
    public static class BlockText
    {
        public static void Show(TMP_Text text, int block)
        {
            if (block > 0)
            {
                text.SetText("{0}", block);
                text.color = UiStyle.TextColor;
            }
            else
            {
                text.SetText("×");
                text.color = UiStyle.Danger;
            }
        }
    }
}
