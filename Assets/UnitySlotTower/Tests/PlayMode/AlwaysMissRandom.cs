using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.PlayMode
{
    /// <summary>
    /// スロットの抽選を必ずハズレにする（払い出しでコインが増えない）。シーン読み込み前に
    /// <c>SlotCabinet.RandomOverride</c> に入れ、TearDown で null に戻す。
    /// </summary>
    internal sealed class AlwaysMissRandom : IRandom
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;

        public double NextDouble() => 0.0;
    }
}
