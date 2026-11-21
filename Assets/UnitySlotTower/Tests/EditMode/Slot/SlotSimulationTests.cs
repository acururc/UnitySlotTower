using System;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>AC-108: 除外後の出目の比率を固定シードで確かめる。</summary>
    public sealed class SlotSimulationTests
    {
        private const int Games = 20000;
        private const double Tolerance = 0.015;

        [TestCase(new SlotSymbol[] { })]
        [TestCase(new[] { SlotSymbol.Blue7 })]
        [TestCase(new[] { SlotSymbol.Yellow7 })]
        [TestCase(new[] { SlotSymbol.Bar })]
        [TestCase(new[] { SlotSymbol.Coin })]
        [TestCase(new[] { SlotSymbol.Blue7, SlotSymbol.Yellow7, SlotSymbol.Bar })]
        public void Outcome_Rates_Follow_Remaining_Weights(SlotSymbol[] excluded)
        {
            var patterns = SlotPatterns.Load(SlotTestRules.ProjectTable(SlotPatterns.FileName));
            var reels = SlotReels.Load(SlotTestRules.ProjectTable(SlotReels.FileName));
            var machine = new SlotMachine(reels, patterns.Patterns, new CoinWallet(Games), 1, new SeededRandom(42));
            Func<SlotSymbol, bool> filter = null;
            if (excluded.Length > 0)
            {
                filter = symbol => !excluded.Contains(symbol);
            }

            var weights = new[] { 3, 3, 3, 3, 4, 4 };
            foreach (var symbol in excluded)
            {
                weights[(int)symbol] = 0;
            }

            var counts = new int[SlotSymbols.Count];
            for (var i = 0; i < Games; i++)
            {
                machine.TryLever(filter);
                Assert.That(machine.Outcome.HasValue, Is.True, "ハズレが出た");
                counts[(int)machine.Outcome.Value]++;
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
                    Assert.That((double)counts[i] / Games,
                        Is.EqualTo((double)weights[i] / total).Within(Tolerance), SlotSymbols.Ids[i]);
                }
            }
        }
    }
}
