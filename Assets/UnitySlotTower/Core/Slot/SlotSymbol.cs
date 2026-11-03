using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>リールの図柄（D-10、plan 5章 スロット）。7 と BAR を先頭に置く（筐体の図柄の子の並びと合わせる）。</summary>
    public enum SlotSymbol
    {
        Seven,
        Bar,
        Atk,
        Speed,
        Heal,
        Coin,
    }

    public static class SlotSymbols
    {
        public const int Count = 6;

        /// <summary>CSV の図柄 ID（<c>seven</c>・<c>bar</c>・<c>atk</c>・<c>speed</c>・<c>heal</c>・<c>coin</c>）。</summary>
        public static readonly string[] Ids = { "seven", "bar", "atk", "speed", "heal", "coin" };

        /// <summary>CSV の図柄 ID を読む。</summary>
        public static SlotSymbol Parse(CsvRow row, string column)
        {
            var id = row.Get(column);
            for (var i = 0; i < Ids.Length; i++)
            {
                if (Ids[i] == id)
                {
                    return (SlotSymbol)i;
                }
            }

            throw row.Error(column, $"未知の図柄 '{id}'");
        }

        /// <summary>
        /// 配列に必要な数（D-46）: 7・BAR はちょうど1つ、ほかの4図柄は1つ以上。
        /// </summary>
        public static bool IsCountAllowed(SlotSymbol symbol, int count) =>
            symbol == SlotSymbol.Seven || symbol == SlotSymbol.Bar ? count == 1 : count >= 1;
    }
}
