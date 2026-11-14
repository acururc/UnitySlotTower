using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>固定シードの大量試行で、出目の出現率が設定ごとの20パターンの表どおりか確かめる（AC-040）。</summary>
    public sealed class SlotSimulationTests
    {
        private const int Games = 100000;
        private const double Tolerance = 0.01;

        private static SlotPatterns Patterns() => SlotPatterns.Load(SlotTestRules.ProjectTable(SlotPatterns.FileName));

        private static SlotReels Reels() => SlotReels.Load(SlotTestRules.ProjectTable(SlotReels.FileName));

        private static SlotMachine Machine(int setting, int games, ulong seed) =>
            new SlotMachine(Reels(), Patterns().For(setting), new CoinWallet(games * 3), 3, new SeededRandom(seed));

        /// <summary>出目ごとの回数（添字は図柄、最後はハズレ）。</summary>
        private static int[] Play(int setting, int games, ulong seed)
        {
            var machine = Machine(setting, games, seed);
            var counts = new int[SlotSymbols.Count + 1];
            for (var i = 0; i < games; i++)
            {
                machine.TryLever();
                counts[machine.Outcome.HasValue ? (int)machine.Outcome.Value : SlotSymbols.Count]++;
                machine.Settle();
            }

            return counts;
        }

        [TestCase(1)]
        [TestCase(6)]
        [Description("AC-040: 各出目の出現率は 個数 ÷ 20 に ±1% で一致する（設定1: 色7 各15%・ハズレ20%・バー10%・コイン10%）")]
        public void Outcome_Rates_Match_The_Pattern_Table(int setting)
        {
            var patterns = Patterns();
            var counts = Play(setting, Games, 42);

            var off = new List<string>();
            for (var i = 0; i <= SlotSymbols.Count; i++)
            {
                var outcome = i < SlotSymbols.Count ? (SlotSymbol?)i : null;
                var expected = patterns.Count(setting, outcome) / 20.0;
                var actual = (double)counts[i] / Games;
                if (System.Math.Abs(actual - expected) > Tolerance)
                {
                    off.Add($"{(outcome.HasValue ? SlotSymbols.Ids[i] : "miss")}: {actual:P2} (表 {expected:P0})");
                }
            }

            Assert.That(off, Is.Empty);
        }

        [Test]
        [Description("AC-040: 設定1の赤7は 3/20 = 15%（手計算の値）")]
        public void Setting_1_Red_Seven_Rate_Is_15_Percent()
        {
            var counts = Play(1, Games, 7);

            Assert.That((double)counts[(int)SlotSymbol.Red7] / Games, Is.EqualTo(0.15).Within(Tolerance));
        }

        [Test]
        [Description("AC-040, FR-045: 設定を変えても、掛け金と揃いの止まる位置（表以外）は変わらない")]
        public void Setting_Changes_Only_The_Draw()
        {
            var results = new List<string>();
            for (var setting = 1; setting <= 6; setting++)
            {
                var wallet = new CoinWallet(3000);
                var machine = new SlotMachine(Reels(), Patterns().For(setting), wallet, 3, new SeededRandom(9));
                var stops = new SortedDictionary<int, string>();
                for (var i = 0; i < 1000; i++)
                {
                    machine.TryLever();
                    if (machine.Outcome.HasValue)
                    {
                        stops[(int)machine.Outcome.Value] =
                            $"{machine.StopPosition(0)}{machine.StopPosition(1)}{machine.StopPosition(2)}";
                    }

                    machine.Settle();
                }

                results.Add($"spent {3000 - wallet.Coins} " + string.Join(" ", stops.Select(p => $"{p.Key}:{p.Value}")));
            }

            Assert.That(results.Distinct().Count(), Is.EqualTo(1), string.Join("\n", results));
        }
    }
}
