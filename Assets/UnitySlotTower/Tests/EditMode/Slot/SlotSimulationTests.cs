using System;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>spec 011 AC-002: ラインの揃う割合と図柄の比率を固定シードで確かめる。</summary>
    public sealed class SlotSimulationTests
    {
        private const int Games = 20000;
        private const double Tolerance = 0.015;
        private const double HitRate = 0.6;

        private static SlotMachine ProjectMachine(int bet, ulong seed = 42)
        {
            var patterns = SlotPatterns.Load(SlotTestRules.ProjectTable(SlotPatterns.FileName));
            var reels = SlotReels.Load(SlotTestRules.ProjectTable(SlotReels.FileName));
            return new SlotMachine(reels, patterns.Patterns, new CoinWallet(Games * bet), new SeededRandom(seed), HitRate)
            {
                SelectedBet = bet,
            };
        }

        [Test]
        [Description("spec 011 AC-002: 1枚のとき中段が揃う割合は line_hit_rate に近い")]
        public void Single_Line_Hit_Rate_Matches_The_Setting()
        {
            var machine = ProjectMachine(1);
            var hits = 0;
            for (var i = 0; i < Games; i++)
            {
                machine.TryLever();
                hits += machine.Hits.Count;
                machine.Settle();
            }

            Assert.That((double)hits / Games, Is.EqualTo(HitRate).Within(Tolerance));
        }

        [Test]
        [Description("spec 011 AC-002: 3枚のとき、どれか揃う割合は 1-(1-p)^3、二重以上は p^2(3-2p) に近い（図柄が尽きないので独立）")]
        public void Three_Lines_Hit_Rates_Match_Independent_Lines()
        {
            var machine = ProjectMachine(3);
            int any = 0, multi = 0;
            for (var i = 0; i < Games; i++)
            {
                machine.TryLever();
                any += machine.Hits.Count > 0 ? 1 : 0;
                multi += machine.Hits.Count >= 2 ? 1 : 0;
                machine.Settle();
            }

            Assert.That((double)any / Games, Is.EqualTo(1 - Math.Pow(1 - HitRate, 3)).Within(Tolerance));
            Assert.That((double)multi / Games, Is.EqualTo(HitRate * HitRate * (3 - 2 * HitRate)).Within(Tolerance));
        }

        [TestCase(new SlotSymbol[] { })]
        [TestCase(new[] { SlotSymbol.Blue7 })]
        [TestCase(new[] { SlotSymbol.Coin })]
        [TestCase(new[] { SlotSymbol.Blue7, SlotSymbol.Yellow7, SlotSymbol.Bar })]
        [Description("spec 011 FR-010: 1枚のとき、揃った図柄は出せる図柄の個数を重みに選ばれる")]
        public void Symbol_Rates_Follow_Remaining_Weights(SlotSymbol[] excluded)
        {
            var machine = ProjectMachine(1);
            Func<SlotSymbol, bool> filter = symbol => !excluded.Contains(symbol);
            var weights = new[] { 3, 3, 3, 3, 4, 4 };
            foreach (var symbol in excluded)
            {
                weights[(int)symbol] = 0;
            }

            var counts = new int[SlotSymbols.Count];
            var hits = 0;
            for (var i = 0; i < Games; i++)
            {
                machine.TryLever(filter);
                foreach (var hit in machine.Hits)
                {
                    counts[(int)hit.Symbol]++;
                    hits++;
                }

                machine.Settle();
            }

            var total = weights.Sum();
            for (var i = 0; i < weights.Length; i++)
            {
                if (weights[i] == 0)
                {
                    Assert.That(counts[i], Is.Zero, SlotSymbols.Ids[i]);
                }
                else
                {
                    Assert.That((double)counts[i] / hits,
                        Is.EqualTo((double)weights[i] / total).Within(Tolerance), SlotSymbols.Ids[i]);
                }
            }
        }
    }
}
