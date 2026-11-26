using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.PlayMode
{
    /// <summary>
    /// 中段を必ず揃え（NextDouble 0 &lt; line_hit_rate）、重みの合計20のうち12番目＝最初のバー（色7各3個の直後）を選ぶ（spec 011）。シーン読み込み前に
    /// <c>SlotCabinet.RandomOverride</c> に入れ、TearDown で null に戻す。
    /// </summary>
    internal sealed class BarPatternRandom : IRandom
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive == 0 && maxExclusive == 20 ? 12 : minInclusive;

        public double NextDouble() => 0.0;
    }
}
