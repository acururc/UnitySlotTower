using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>spec 011 AC-001・AC-002: 賭け枚数とライン抽選、見える図柄。</summary>
    public sealed class SlotMachineTests
    {
        private static readonly SlotSymbol[] AllSymbols =
        {
            SlotSymbol.Red7, SlotSymbol.Blue7, SlotSymbol.Green7, SlotSymbol.Yellow7, SlotSymbol.Bar, SlotSymbol.Coin,
        };

        private static readonly SlotSymbol?[] EvenPatterns =
            AllSymbols.SelectMany(s => Enumerable.Repeat((SlotSymbol?)s, 3)).Concat(new SlotSymbol?[] { null, null }).ToArray();

        private static SlotMachine Machine(double hitRate = 0.6, int coins = 30, int bet = 1, ulong seed = 1,
            IReadOnlyList<SlotSymbol?> patterns = null) =>
            Machine(new CoinWallet(coins), hitRate, bet, seed, patterns);

        private static SlotMachine Machine(CoinWallet wallet, double hitRate = 0.6, int bet = 1, ulong seed = 1,
            IReadOnlyList<SlotSymbol?> patterns = null) =>
            new SlotMachine(SlotTestRules.Reels(), patterns ?? EvenPatterns, wallet, new SeededRandom(seed), hitRate)
            {
                SelectedBet = bet,
            };

        private static SlotSymbol[] Row(SlotMachine machine, int row) =>
            Enumerable.Range(0, SlotMachine.ReelCount).Select(reel => machine.Visible(reel, row)).ToArray();

        private static bool IsAligned(SlotMachine machine, int row)
        {
            var symbols = Row(machine, row);
            return symbols.All(s => s == symbols[0]);
        }

        // 見える図柄が抽選結果と一致する（FR-011）: 揃う行は図柄どおり、それ以外の行は揃わない、各リールの縦は別々。
        private static void AssertGridMatchesHits(SlotMachine machine)
        {
            for (var row = -1; row <= 1; row++)
            {
                var hit = machine.Hits.Where(h => h.Row == row).ToArray();
                if (hit.Length == 1)
                {
                    Assert.That(Row(machine, row), Is.All.EqualTo(hit[0].Symbol), $"行 {row}");
                }
                else
                {
                    Assert.That(IsAligned(machine, row), Is.False, $"行 {row} が揃って見える");
                }
            }

            for (var reel = 0; reel < SlotMachine.ReelCount; reel++)
            {
                var column = new[] { machine.Visible(reel, -1), machine.Visible(reel, 0), machine.Visible(reel, 1) };
                Assert.That(column, Is.Unique, $"リール {reel} の縦");
            }
        }

        [Test]
        [Description("FR-001: BET は 1→2→3→1 と切り替わる")]
        public void CycleBet_Goes_One_Two_Three_One()
        {
            var machine = Machine();
            var seen = new List<int> { machine.SelectedBet };
            for (var i = 0; i < 3; i++)
            {
                machine.CycleBet();
                seen.Add(machine.SelectedBet);
            }

            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 3, 1 }));
        }

        [Test]
        [Description("FR-001: 回転中は賭け枚数を変えられない")]
        public void Bet_Does_Not_Change_While_Spinning()
        {
            var machine = Machine(bet: 2);
            machine.TryLever();
            machine.CycleBet();
            machine.SelectedBet = 3;

            Assert.That(machine.SelectedBet, Is.EqualTo(2));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [Description("FR-003: レバーで賭け枚数ぶんのコインを払う")]
        public void Lever_Spends_The_Bet(int bet)
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(wallet, bet: bet);

            Assert.That(machine.TryLever(), Is.EqualTo(LeverResult.Started));
            Assert.That((wallet.Coins, machine.SpinBet), Is.EqualTo((10 - bet, bet)));
        }

        [TestCase(2)]
        [TestCase(1)]
        [Description("FR-003: コインが賭け枚数より少ないと、持っている枚数まで下げて払う（選んだ枚数は残る）")]
        public void Short_Coins_Lower_The_Bet(int coins)
        {
            var wallet = new CoinWallet(coins);
            var machine = Machine(wallet, bet: 3);

            Assert.That(machine.EffectiveBet, Is.EqualTo(coins));
            machine.TryLever();
            Assert.That((machine.SpinBet, wallet.Coins, machine.SelectedBet), Is.EqualTo((coins, 0, 3)));
        }

        [Test]
        [Description("FR-003: コインが0ならレバーは引けない")]
        public void Lever_Without_Coins_Is_Rejected()
        {
            var machine = Machine(coins: 0);

            Assert.That((machine.TryLever(), machine.IsSpinning), Is.EqualTo((LeverResult.NotEnoughCoins, false)));
        }

        [Test]
        [Description("spec 015 FR-001: コインを使わない1回はコイン0でも選んだ枚数で引け、コインは減らず、次からは普通に払う")]
        public void Free_Spin_Uses_No_Coins_Once()
        {
            var wallet = new CoinWallet(0);
            var machine = Machine(wallet, bet: 3);
            machine.NextSpinFree = true;

            Assert.That((machine.TryLever(), machine.SpinBet, wallet.Coins, machine.NextSpinFree),
                Is.EqualTo((LeverResult.Started, 3, 0, false)));
            machine.Settle();
            Assert.That(machine.TryLever(), Is.EqualTo(LeverResult.NotEnoughCoins));
        }

        [Test]
        public void Lever_While_Spinning_Is_Rejected()
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(wallet);
            machine.TryLever();

            Assert.That((machine.TryLever(), wallet.Coins), Is.EqualTo((LeverResult.Spinning, 9)));
        }

        [Test]
        public void Lever_Can_Be_Pulled_Again_After_Settle()
        {
            var machine = Machine();
            machine.TryLever();
            machine.Settle();

            Assert.That(machine.TryLever(), Is.EqualTo(LeverResult.Started));
        }

        [Test]
        public void Settle_While_Not_Spinning_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Machine().Settle());
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [Description("FR-010: 揃うのは有効ラインだけ（1枚=中段、2枚=＋上段、3枚=＋下段）")]
        public void Hits_Only_On_Active_Lines(int bet)
        {
            var machine = Machine(hitRate: 1, coins: 300, bet: bet);
            for (var i = 0; i < 100; i++)
            {
                machine.TryLever();
                Assert.That(machine.Hits.Select(h => h.Row), Is.EqualTo(SlotMachine.LineRows.Take(bet)));
                machine.Settle();
            }
        }

        [Test]
        [Description("FR-010: 揃う確率0なら何も揃わない")]
        public void Zero_Hit_Rate_Never_Hits()
        {
            var machine = Machine(hitRate: 0, coins: 300, bet: 3);
            for (var i = 0; i < 100; i++)
            {
                machine.TryLever();
                Assert.That(machine.Hits, Is.Empty);
                machine.Settle();
            }
        }

        [Test]
        [Description("FR-010 / D3: 1回で同じ図柄が2ラインに揃わない")]
        public void Same_Symbol_Never_Hits_Two_Lines()
        {
            var machine = Machine(hitRate: 1, coins: 900, bet: 3);
            for (var i = 0; i < 300; i++)
            {
                machine.TryLever();
                Assert.That(machine.Hits.Select(h => h.Symbol), Is.Unique);
                machine.Settle();
            }
        }

        [Test]
        [Description("FR-010: 発動中の図柄（フィルタで外した図柄）は揃わない。候補が尽きたラインは揃わない")]
        public void Filtered_Symbols_Do_Not_Hit_And_Lines_Without_Candidates_Miss()
        {
            var machine = Machine(hitRate: 1, coins: 300, bet: 3);
            for (var i = 0; i < 100; i++)
            {
                machine.TryLever(s => s == SlotSymbol.Red7 || s == SlotSymbol.Coin);
                Assert.That(machine.Hits.Select(h => h.Symbol), Is.EquivalentTo(new[] { SlotSymbol.Red7, SlotSymbol.Coin }));
                Assert.That(machine.Hits.Select(h => h.Row), Is.EqualTo(new[] { 0, -1 }));
                machine.Settle();
            }
        }

        [Test]
        [Description("FR-010: 抽選表にない図柄（個数0）は揃わない。ハズレの個数は使わない")]
        public void Symbols_Without_Weight_Never_Hit()
        {
            var patterns = Enumerable.Repeat((SlotSymbol?)SlotSymbol.Bar, 5).Concat(Enumerable.Repeat((SlotSymbol?)null, 15)).ToArray();
            var machine = Machine(hitRate: 1, coins: 50, bet: 1, patterns: patterns);
            for (var i = 0; i < 50; i++)
            {
                machine.TryLever();
                Assert.That(machine.Hits.Select(h => h.Symbol), Is.EqualTo(new[] { SlotSymbol.Bar }));
                machine.Settle();
            }
        }

        [TestCase(1, 0.0)]
        [TestCase(1, 1.0)]
        [TestCase(2, 0.6)]
        [TestCase(3, 0.6)]
        [TestCase(3, 1.0)]
        [TestCase(3, 0.0)]
        [Description("FR-011: 止まった図柄は抽選結果と一致する（無効ラインも揃って見えない）")]
        public void Visible_Grid_Matches_Hits(int bet, double hitRate)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var machine = Machine(hitRate: hitRate, coins: 3, bet: bet, seed: seed);
                machine.TryLever();
                AssertGridMatchesHits(machine);
            }
        }

        [Test]
        [Description("FR-012: Settle で揃ったラインを中段→上段→下段の順に通知する")]
        public void Settle_Notifies_Hits_In_Line_Order()
        {
            var machine = Machine(hitRate: 1, bet: 3);
            var notified = new List<SlotLineHit>();
            machine.LineAligned += notified.Add;
            machine.TryLever();
            Assert.That(notified, Is.Empty);

            machine.Settle();

            Assert.That(notified.Select(h => h.Row), Is.EqualTo(new[] { 0, -1, 1 }));
            Assert.That(notified, Is.EqualTo(machine.Hits));
        }

        [Test]
        [Description("FR-032: 決め打ちは抽選せずその揃いにする（中→上→下の順に並べる）")]
        public void Forced_Hits_Override_The_Draw()
        {
            var machine = Machine(hitRate: 0, coins: 3, bet: 3);
            var forced = new[]
            {
                new SlotLineHit(1, SlotSymbol.Bar), new SlotLineHit(0, SlotSymbol.Red7), new SlotLineHit(-1, SlotSymbol.Blue7),
            };

            Assert.That(machine.TryLever(null, forced), Is.EqualTo(LeverResult.Started));

            Assert.That(machine.Hits, Is.EqualTo(new[] { forced[1], forced[2], forced[0] }));
            AssertGridMatchesHits(machine);
        }

        [Test]
        public void Forced_Hits_Reject_Duplicates()
        {
            var machine = Machine(coins: 3, bet: 3);
            var forced = new[] { new SlotLineHit(0, SlotSymbol.Red7), new SlotLineHit(-1, SlotSymbol.Red7) };

            Assert.Throws<ArgumentException>(() => machine.TryLever(null, forced));
        }

        [Test]
        [Description("FR-041: 決まりに合わない並びでは台を作れない")]
        public void Machine_Rejects_Invalid_Reels()
        {
            var reels = SlotTestRules.Reels(SlotTestRules.PlanLeft, SlotTestRules.PlanLeft, SlotTestRules.PlanLeft);

            Assert.Throws<ArgumentException>(() =>
                new SlotMachine(reels, EvenPatterns, new CoinWallet(10), new SeededRandom(1), 0.6));
        }
    }
}
