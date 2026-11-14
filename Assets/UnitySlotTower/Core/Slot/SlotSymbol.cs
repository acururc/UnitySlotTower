using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>リールの図柄（D-61、plan 16.3）。色7の4つ・バー・コイン。</summary>
    public enum SlotSymbol
    {
        Red7,
        Blue7,
        Green7,
        Yellow7,
        Bar,
        Coin,
    }

    public static class SlotSymbols
    {
        public const int Count = 6;

        /// <summary>CSV の図柄 ID（<see cref="SlotSymbol"/> の順）。</summary>
        public static readonly string[] Ids = { "red7", "blue7", "green7", "yellow7", "bar", "coin" };

        /// <summary>抽選表の出目でハズレを表す ID。</summary>
        public const string MissId = "miss";

        /// <summary>CSV の図柄 ID を読む。</summary>
        public static SlotSymbol Parse(CsvRow row, string column)
        {
            var id = row.Get(column);
            var index = System.Array.IndexOf(Ids, id);
            if (index < 0)
            {
                throw row.Error(column, $"未知の図柄 '{id}'");
            }

            return (SlotSymbol)index;
        }

        /// <summary>抽選表の出目（図柄 ID か <see cref="MissId"/>）を読む。ハズレは null。</summary>
        public static SlotSymbol? ParseOutcome(CsvRow row, string column) =>
            row.Get(column) == MissId ? (SlotSymbol?)null : Parse(row, column);
    }
}
