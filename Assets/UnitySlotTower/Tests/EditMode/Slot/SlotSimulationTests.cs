using System;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>固定シードの大量試行で、ハズレ率と当たり時の図柄の出現率を確かめる（AC-040）。</summary>
    public sealed class SlotSimulationTests
    {
        private const int Games = 100000;
        private const double Tolerance = 0.01;

        // plan 5章の既定配列（7・BAR 各1、攻撃・速度・回復 各2、コイン4）。
        private const string DefaultReel = "seven atk coin heal speed coin bar atk coin heal speed coin";

        // コインに偏った配列（コイン 7/12）。
        private const string CoinHeavyReel = "seven bar atk speed heal coin coin coin coin coin coin coin";

        private static ReelLayout Layout(string ids) =>
            new ReelLayout(ids.Split(' ').Select(id => (SlotSymbol)Array.IndexOf(SlotSymbols.Ids, id)).ToArray());

        /// <summary><paramref name="games"/> 回まわして、ハズレの回数と当たり図柄ごとの回数を返す。</summary>
        private static (int misses, int[] hits) Play(ReelLayout layout, double missRate, int games, ulong seed)
        {
            var machine = new SlotMachine(layout, missRate, new CoinWallet(games * 3), 3, SlotTestRules.NoBonus(),
                new SeededRandom(seed));
            var misses = 0;
            var hits = new int[SlotSymbols.Count];
            for (var i = 0; i < games; i++)
            {
                machine.TryLever();
                if (machine.Result.HasValue)
                {
                    hits[(int)machine.Result.Value]++;
                }
                else
                {
                    misses++;
                }

                for (var reel = 0; reel < SlotMachine.ReelCount; reel++)
                {
                    machine.Stop(reel, i);
                }
            }

            return (misses, hits);
        }

        [TestCase(0.85)]
        [TestCase(0.70)]
        [Description("AC-040, FR-045: ハズレ率がデータ値（設定1 85%・設定6 70%）に一致する")]
        public void Miss_Rate_Matches_The_Setting(double missRate)
        {
            var (misses, _) = Play(Layout(DefaultReel), missRate, Games, 11);

            Assert.That((double)misses / Games, Is.EqualTo(missRate).Within(Tolerance));
        }

        [TestCase(0.85, DefaultReel, new[] { 1, 1, 2, 2, 2, 4 })]
        [TestCase(0.70, DefaultReel, new[] { 1, 1, 2, 2, 2, 4 })]
        [TestCase(0.70, CoinHeavyReel, new[] { 1, 1, 1, 1, 1, 7 })]
        [Description("AC-040, D-43: 当たり時の図柄の出現率が配列のマス数 ÷ 12 に一致し、設定では変わらない（順は 7・BAR・攻・速・回・コ）")]
        public void Hit_Symbols_Follow_The_Layout_Ratio(double missRate, string reel, int[] cellsPerSymbol)
        {
            var (_, hits) = Play(Layout(reel), missRate, Games * 2, 23);
            var total = hits.Sum();

            Assert.That(Enumerable.Range(0, SlotSymbols.Count).Select(s => (double)hits[s] / total),
                Is.EqualTo(cellsPerSymbol.Select(c => c / 12.0)).Within(Tolerance));
        }
    }
}
