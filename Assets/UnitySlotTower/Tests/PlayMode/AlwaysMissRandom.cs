using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.PlayMode
{
    /// <summary>
    /// 20パターンのうち最初のバー（色7各3個の直後）を選ぶ。シーン読み込み前に
    /// <c>SlotCabinet.RandomOverride</c> に入れ、TearDown で null に戻す。
    /// </summary>
    internal sealed class BarPatternRandom : IRandom
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive == 0 && maxExclusive == 20 ? 12 : minInclusive;

        public double NextDouble() => 0.0;
    }
}
