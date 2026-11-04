using System;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 一時停止中の説明（T404、FR-012）に出す通常時の1ゲームあたりの確率。ハズレ率と、図柄ごとの「当たり率 × マス数 ÷ 総マス数」（D-43）。
    /// 表示用に小数1桁へ四捨五入した 1/10 % 単位で返す。CSV の値（0.79 など）をそのまま 10 進で計算し、
    /// 2 進の誤差で 17.5 が 17 に落ちないようにする。四捨五入するので合計が 100.0% にならないことがある。
    /// </summary>
    public static class SlotOdds
    {
        public static int MissTenths(double missRate) => Round((decimal)missRate * 1000m);

        public static int ChanceTenths(ReelLayout layout, double missRate, SlotSymbol symbol) =>
            Round((1m - (decimal)missRate) * layout.Count(symbol) * 1000m / layout.Length);

        private static int Round(decimal tenths) => (int)Math.Round(tenths, MidpointRounding.AwayFromZero);
    }
}
