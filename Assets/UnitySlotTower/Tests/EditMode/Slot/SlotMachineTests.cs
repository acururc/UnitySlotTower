using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotMachineTests
    {
        private static readonly SlotSymbol[] AllSymbols =
        {
            SlotSymbol.Red7, SlotSymbol.Blue7, SlotSymbol.Green7, SlotSymbol.Yellow7, SlotSymbol.Bar, SlotSymbol.Coin,
        };

        /// <summary>20パターンすべてが <paramref name="outcome"/>（null はハズレ）の台。</summary>
        private static SlotMachine Machine(SlotSymbol? outcome, int coins = 30, ulong seed = 1) =>
            Machine(Enumerable.Repeat(outcome, 20).ToArray(), new CoinWallet(coins), seed);

        private static SlotMachine Machine(IReadOnlyList<SlotSymbol?> patterns, CoinWallet wallet, ulong seed = 1) =>
            new SlotMachine(SlotTestRules.Reels(), patterns, wallet, 3, new SeededRandom(seed));

        private static SlotSymbol[] Row(SlotMachine machine, int row) =>
            Enumerable.Range(0, SlotMachine.ReelCount).Select(reel => machine.Visible(reel, row)).ToArray();

        private static bool Aligned(SlotSymbol[] row) => row.All(s => s == row[0]);

        [Test]
        [Description("AC-041: 回すとコインが3枚減る")]
        public void Lever_Spends_The_Bet()
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(new SlotSymbol?[] { null }, wallet);

            machine.TryLever();

            Assert.That(wallet.Coins, Is.EqualTo(7));
        }

        [Test]
        [Description("AC-041: コインが3枚未満だと回せず、コインは減らない")]
        public void Lever_Without_Enough_Coins_Is_Rejected()
        {
            var wallet = new CoinWallet(2);
            var machine = Machine(new SlotSymbol?[] { null }, wallet);

            var result = machine.TryLever();

            Assert.That((result, wallet.Coins, machine.IsSpinning), Is.EqualTo((LeverResult.NotEnoughCoins, 2, false)));
        }

        [Test]
        [Description("plan 16.3: 回転中のレバーは受け付けず、コインも減らない")]
        public void Lever_While_Spinning_Is_Rejected()
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(new SlotSymbol?[] { null }, wallet);
            machine.TryLever();

            var result = machine.TryLever();

            Assert.That((result, wallet.Coins), Is.EqualTo((LeverResult.Spinning, 7)));
        }

        [Test]
        public void Lever_Can_Be_Pulled_Again_After_Settle()
        {
            var machine = Machine(SlotSymbol.Coin);
            machine.TryLever();
            machine.Settle();

            Assert.That(machine.TryLever(), Is.EqualTo(LeverResult.Started));
        }

        [Test]
        [Description("AC-041: 揃いの出目は中段にその図柄が3つ並ぶ")]
        public void Aligned_Outcome_Shows_Three_In_The_Middle([ValueSource(nameof(AllSymbols))] SlotSymbol symbol)
        {
            var machine = Machine(symbol);

            machine.TryLever();

            Assert.That(Row(machine, 0), Is.EqualTo(new[] { symbol, symbol, symbol }));
        }

        [Test]
        [Description("AC-041: 当たりのとき上段・下段は揃わない")]
        public void Aligned_Outcome_Does_Not_Align_Top_Or_Bottom([ValueSource(nameof(AllSymbols))] SlotSymbol symbol)
        {
            var machine = Machine(symbol);

            machine.TryLever();

            Assert.That((Aligned(Row(machine, -1)), Aligned(Row(machine, 1))), Is.EqualTo((false, false)));
        }

        [Test]
        [Description("AC-041: ハズレはどの段（上・中・下）も揃わない（固定シードで500回）")]
        public void Miss_Aligns_No_Row()
        {
            var machine = Machine((SlotSymbol?)null, coins: 1500);
            var aligned = 0;
            for (var i = 0; i < 500; i++)
            {
                machine.TryLever();
                if (Aligned(Row(machine, -1)) || Aligned(Row(machine, 0)) || Aligned(Row(machine, 1)))
                {
                    aligned++;
                }

                machine.Settle();
            }

            Assert.That(aligned, Is.Zero);
        }

        [Test]
        [Description("plan 16.3: ハズレの止まり方は1通りに決め打ちしない（500回で10通り以上）")]
        public void Miss_Stops_Vary()
        {
            var machine = Machine((SlotSymbol?)null, coins: 1500);
            var seen = new HashSet<(int, int, int)>();
            for (var i = 0; i < 500; i++)
            {
                machine.TryLever();
                seen.Add((machine.StopPosition(0), machine.StopPosition(1), machine.StopPosition(2)));
                machine.Settle();
            }

            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void Outcome_Is_The_Drawn_Pattern()
        {
            var machine = Machine(SlotSymbol.Green7);

            machine.TryLever();

            Assert.That(machine.Outcome, Is.EqualTo(SlotSymbol.Green7));
        }

        [Test]
        [Description("FR-042: 止まった（Settle）ときに揃った図柄を通知する")]
        public void Settle_Notifies_The_Aligned_Symbol()
        {
            var machine = Machine(SlotSymbol.Bar);
            var aligned = new List<SlotSymbol>();
            machine.Aligned += aligned.Add;
            machine.TryLever();
            var beforeSettle = aligned.Count;

            machine.Settle();

            Assert.That((beforeSettle, string.Join(",", aligned)), Is.EqualTo((0, "Bar")));
        }

        [Test]
        public void Settle_After_A_Miss_Notifies_Nothing()
        {
            var machine = Machine((SlotSymbol?)null);
            var count = 0;
            machine.Aligned += _ => count++;
            machine.TryLever();

            machine.Settle();

            Assert.That((count, machine.IsSpinning), Is.EqualTo((0, false)));
        }

        [Test]
        public void Settle_While_Not_Spinning_Throws()
        {
            var machine = Machine(SlotSymbol.Coin);

            Assert.Throws<InvalidOperationException>(() => machine.Settle());
        }

        [Test]
        [Description("AC-041, D-68: 回数固定ではコインが減らない")]
        public void Fixed_Spins_Do_Not_Spend_Coins()
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(new SlotSymbol?[] { null }, wallet);
            machine.StartFixedSpins(6);

            machine.TryLever();

            Assert.That((wallet.Coins, machine.FixedSpinsLeft), Is.EqualTo((10, (int?)5)));
        }

        [Test]
        [Description("AC-041, D-68: 回数固定は決まった回数で止まり、コインがあっても回せない")]
        public void Fixed_Spins_Stop_After_The_Count()
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(new SlotSymbol?[] { null }, wallet);
            machine.StartFixedSpins(2);
            for (var i = 0; i < 2; i++)
            {
                machine.TryLever();
                machine.Settle();
            }

            Assert.That((machine.TryLever(), wallet.Coins), Is.EqualTo((LeverResult.NoSpinsLeft, 10)));
        }

        [Test]
        [Description("D-68: 回数固定でもコインが0枚で回せる")]
        public void Fixed_Spins_Work_Without_Coins()
        {
            var machine = Machine(new SlotSymbol?[] { null }, new CoinWallet(0));
            machine.StartFixedSpins(1);

            Assert.That(machine.TryLever(), Is.EqualTo(LeverResult.Started));
        }

        [Test]
        public void Ending_Fixed_Spins_Goes_Back_To_Coins()
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(new SlotSymbol?[] { null }, wallet);
            machine.StartFixedSpins(1);
            machine.EndFixedSpins();

            machine.TryLever();

            Assert.That((wallet.Coins, machine.FixedSpinsLeft), Is.EqualTo((7, (int?)null)));
        }

        [Test]
        [Description("FR-041: 決まりに合わない並びでは台を作れない")]
        public void Machine_Rejects_Invalid_Reels()
        {
            var reels = SlotTestRules.Reels(SlotTestRules.PlanLeft, SlotTestRules.PlanLeft, SlotTestRules.PlanLeft);

            Assert.Throws<ArgumentException>(() =>
                new SlotMachine(reels, new SlotSymbol?[] { null }, new CoinWallet(10), 3, new SeededRandom(1)));
        }
    }
}
